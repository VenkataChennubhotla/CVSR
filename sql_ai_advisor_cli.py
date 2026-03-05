"""
SQL AI Advisor - Single-file CLI (Windows-friendly, read-only safe)

Menu (Option Set A):
1) SQL query
2) Table(s)
3) Execution plan (.sqlplan or plan XML)
4) Full database health check (DB-scoped + some server signals)
5) DB usage scan (last 30 days) [decommission helper]
   - Sub-options: All DBs or Single DB
6) Historical DB size growth trend (SINGLE DB, graph-only on console)
   - Uses msdb backup history (backupset + backupfile) as a proxy for DB file size trend.
   - Graphs: console ASCII sparkline; HTML inline SVG.
7) Future DB size growth forecast (next 1 or 2 years; Auto-ARIMA) based on LOCAL snapshot history (sys.master_files -> jsonl)
   - Captures a snapshot now (so you can build history over time)
   - Requires >= 14 distinct days of snapshots per DB to forecast (Auto-ARIMA)
   - Graphs: history + forecast

Notes:
- Read-only against SQL Server: uses SELECT/DMVs only (no CREATE/ALTER/DROP).
- Writes only local files: HTML report, JSON report, local history JSONL file.
- Forecasting uses Auto-ARIMA (pmdarima) if installed; otherwise reports a clear error.
- Graphs: console ASCII sparkline; HTML inline SVG (no plotting libraries).
- Console output does NOT print SVG markup (charts are HTML-only).
"""

import os
import re
import sys
import json
import time
import argparse
import getpass
from typing import Any, Dict, Optional, List, Tuple

import pyodbc
import sqlparse
import requests
from dotenv import load_dotenv
from lxml import etree

load_dotenv()

CONN_STR = os.getenv("SQLSERVER_CONN_STR", "").strip()
OLLAMA_URL = os.getenv("OLLAMA_URL", "http://localhost:11434").strip().rstrip("/")
OLLAMA_MODEL = os.getenv("OLLAMA_MODEL", "qwen2.5:7b").strip()

MAX_SQL_CHARS = 12000

# Local history file next to EXE/script
HISTORY_FILE_NAME = "sql_ai_advisor_history.jsonl"

# Forecast requirements/defaults
FORECAST_MIN_DISTINCT_DAYS = 14
FORECAST_DAYS_PER_YEAR = 365


# =========================
# Connection string helpers
# =========================
def _parse_conn_str(conn_str: str) -> Dict[str, str]:
    out: Dict[str, str] = {}
    s = (conn_str or "").strip().strip(";")
    if not s:
        return out
    parts = [p for p in s.split(";") if p.strip()]
    for p in parts:
        if "=" not in p:
            continue
        k, v = p.split("=", 1)
        out[k.strip().lower()] = v.strip()
    return out


def _build_conn_str(parts: Dict[str, str]) -> str:
    order = [
        "driver",
        "server",
        "database",
        "uid",
        "pwd",
        "trusted_connection",
        "encrypt",
        "trustservercertificate",
        "applicationintent",
        "multisubnetfailover",
        "timeout",
    ]
    used = set()
    segments = []

    for k in order:
        if k in parts and parts[k] != "":
            segments.append(f"{k.upper()}={parts[k]}")
            used.add(k)

    for k, v in parts.items():
        if k in used:
            continue
        if v == "":
            continue
        segments.append(f"{k.upper()}={v}")

    return ";".join(segments) + ";"


def _prompt_if_empty(label: str, current: str) -> str:
    if current:
        return current
    return input(f"{label}: ").strip()


def _prompt_conn_details_from_user(base_conn_str: str) -> str:
    parts = _parse_conn_str(base_conn_str)

    parts.setdefault("driver", "{ODBC Driver 18 for SQL Server}")
    parts.setdefault("encrypt", "yes")
    parts.setdefault("trustservercertificate", "yes")

    parts["server"] = _prompt_if_empty("SQL Server (hostname\\instance or host,port)", parts.get("server", ""))
    parts["database"] = _prompt_if_empty("Database", parts.get("database", ""))
    parts["uid"] = _prompt_if_empty("Username", parts.get("uid", ""))

    pwd = getpass.getpass("Password: ")
    parts["pwd"] = pwd
    return _build_conn_str(parts)


def connect() -> pyodbc.Connection:
    runtime_conn_str = _prompt_conn_details_from_user(CONN_STR)
    return pyodbc.connect(runtime_conn_str, autocommit=True)


# =========================
# Safe SQL helpers
# =========================
def safe_query_rows(cur: pyodbc.Cursor, sql: str, params: Tuple[Any, ...] = ()) -> Tuple[Optional[List[Any]], Optional[str]]:
    try:
        rows = cur.execute(sql, params).fetchall()
        return rows, None
    except Exception as e:
        return None, str(e)


def safe_query_one(cur: pyodbc.Cursor, sql: str, params: Tuple[Any, ...] = ()) -> Tuple[Optional[Any], Optional[str]]:
    try:
        row = cur.execute(sql, params).fetchone()
        return row, None
    except Exception as e:
        return None, str(e)


def use_db(cur: pyodbc.Cursor, db_name: str) -> Optional[str]:
    try:
        safe = db_name.replace("]", "]]")
        cur.execute(f"USE [{safe}];")
        return None
    except Exception as e:
        return str(e)


# =========================
# Output helpers
# =========================
def _cell(v: Any) -> str:
    return "" if v is None else str(v)


def print_table(title: str, headers: List[str], rows: List[List[Any]], max_width: int = 70) -> None:
    print(f"\n{title}")
    if not rows:
        print("(no rows)")
        return

    def wrap_cell(text: str, width: int) -> List[str]:
        text = "" if text is None else str(text)
        if width <= 0:
            return [text]
        parts: List[str] = []
        for para in text.splitlines() or [""]:
            p = para
            while len(p) > width:
                cut = p.rfind(" ", 0, width + 1)
                if cut <= 0:
                    cut = width
                parts.append(p[:cut].rstrip())
                p = p[cut:].lstrip()
            parts.append(p)
        return parts or [""]

    srows: List[List[List[str]]] = []
    for r in rows:
        srows.append([wrap_cell(_cell(x), max_width) for x in r])

    col_count = len(headers)
    widths = [len(h) for h in headers]
    for r in srows:
        for i in range(col_count):
            max_line_len = max((len(line) for line in r[i]), default=0)
            widths[i] = max(widths[i], min(max_line_len, max_width))

    def sep(ch: str) -> str:
        return "+" + "+".join(ch * (w + 2) for w in widths) + "+"

    print(sep("-"))
    print("| " + " | ".join(headers[i].ljust(widths[i]) for i in range(col_count)) + " |")
    print(sep("="))

    for r in srows:
        height = max(len(col_lines) for col_lines in r)
        for line_idx in range(height):
            line_cells = []
            for i in range(col_count):
                col_lines = r[i]
                line = col_lines[line_idx] if line_idx < len(col_lines) else ""
                if len(line) > widths[i]:
                    line = line[: widths[i]]
                line_cells.append(line.ljust(widths[i]))
            print("| " + " | ".join(line_cells) + " |")
        print(sep("-"))


def print_table_truncated(title: str, headers: List[str], rows: List[List[Any]], col_max: List[int]) -> None:
    print(f"\n{title}")
    if not rows:
        print("(no rows)")
        return

    col_count = len(headers)
    if len(col_max) != col_count:
        raise ValueError("col_max length must match headers length")

    srows: List[List[str]] = []
    for r in rows:
        rr = []
        for i in range(col_count):
            v = "" if i >= len(r) or r[i] is None else str(r[i])
            m = col_max[i]
            if m and len(v) > m:
                v = v[: max(0, m - 3)] + "..."
            rr.append(v)
        srows.append(rr)

    widths = [len(h) for h in headers]
    for r in srows:
        for i in range(col_count):
            widths[i] = max(widths[i], len(r[i]))

    def sep(ch: str) -> str:
        return "+" + "+".join(ch * (w + 2) for w in widths) + "+"

    print(sep("-"))
    print("| " + " | ".join(headers[i].ljust(widths[i]) for i in range(col_count)) + " |")
    print(sep("="))
    for r in srows:
        print("| " + " | ".join(r[i].ljust(widths[i]) for i in range(col_count)) + " |")
        print(sep("-"))


# =========================
# Graph helpers (console + HTML)
# =========================
def downsample(values: List[float], width: int) -> List[float]:
    if width <= 0 or len(values) <= width:
        return values
    step = len(values) / float(width)
    out = []
    for i in range(width):
        out.append(values[int(i * step)])
    return out


def ascii_sparkline(values: List[float], width: int = 90) -> str:
    ticks = "▁▂▃▄▅▆▇█"
    if not values:
        return ""
    vals = downsample(values, width)
    vmin = min(vals)
    vmax = max(vals)
    if vmax <= vmin:
        return ticks[0] * len(vals)
    chars = []
    for v in vals:
        idx = int((v - vmin) / (vmax - vmin) * (len(ticks) - 1))
        idx = max(0, min(idx, len(ticks) - 1))
        chars.append(ticks[idx])
    return "".join(chars)


def svg_line_chart(
    values: List[float],
    width: int = 900,
    height: int = 180,
    padding: int = 12,
    stroke: str = "#2d6cdf",
    stroke_width: int = 2,
    divider_at_index: Optional[int] = None,
) -> str:
    if not values:
        return "<div class='empty'>(no data)</div>"

    vmin = min(values)
    vmax = max(values)
    if vmax <= vmin:
        vmax = vmin + 1.0

    n = len(values)
    x0 = padding
    y0 = padding
    x1 = width - padding
    y1 = height - padding

    def x(i: int) -> float:
        if n <= 1:
            return float(x0)
        return x0 + (x1 - x0) * (i / float(n - 1))

    def y(v: float) -> float:
        t = (v - vmin) / (vmax - vmin)
        return y1 - t * (y1 - y0)

    pts = " ".join(f"{x(i):.2f},{y(v):.2f}" for i, v in enumerate(values))

    divider_svg = ""
    if divider_at_index is not None and 0 <= divider_at_index < n:
        xd = x(divider_at_index)
        divider_svg = f"<line x1='{xd:.2f}' y1='{y0}' x2='{xd:.2f}' y2='{y1}' stroke='#999' stroke-dasharray='4,4'/>"

    return f"""
    <svg width="{width}" height="{height}" viewBox="0 0 {width} {height}" xmlns="http://www.w3.org/2000/svg" role="img">
      <rect x="0" y="0" width="{width}" height="{height}" fill="#ffffff" stroke="#dddddd"/>
      {divider_svg}
      <polyline fill="none" stroke="{stroke}" stroke-width="{stroke_width}" points="{pts}" />
      <text x="{padding}" y="{height - 4}" font-size="11" fill="#444">min {vmin:.2f} MB</text>
      <text x="{width - padding - 130}" y="{height - 4}" font-size="11" fill="#444">max {max(values):.2f} MB</text>
    </svg>
    """.strip()


# =========================
# HTML report helpers
# =========================
def html_escape(s: Any) -> str:
    s = "" if s is None else str(s)
    return (
        s.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
        .replace("'", "&#39;")
    )


def html_table(title: str, headers: List[str], rows: List[List[Any]]) -> str:
    out = [f"<h2>{html_escape(title)}</h2>"]
    if not rows:
        out.append("<div class='empty'>(no rows)</div>")
        return "\n".join(out)

    out.append("<table>")
    out.append("<thead><tr>" + "".join(f"<th>{html_escape(h)}</th>" for h in headers) + "</tr></thead>")
    out.append("<tbody>")
    for r in rows:
        out.append("<tr>" + "".join(f"<td>{html_escape(c)}</td>" for c in r) + "</tr>")
    out.append("</tbody></table>")
    return "\n".join(out)


def write_html_report(
    path: str,
    sections: List[Tuple[str, List[str], List[List[Any]]]],
    raw_query: str,
    plan_xml: Optional[str],
) -> None:
    css = """
    body { font-family: Segoe UI, Arial, sans-serif; margin: 20px; color: #111; }
    h1 { margin: 0 0 10px 0; }
    h2 { margin-top: 22px; border-bottom: 1px solid #ddd; padding-bottom: 6px; }
    .meta { color: #444; margin-bottom: 16px; }
    table { border-collapse: collapse; width: 100%; font-size: 13px; }
    th, td { border: 1px solid #ddd; padding: 8px; vertical-align: top; }
    th { background: #f6f6f6; text-align: left; }
    tr:nth-child(even) td { background: #fcfcfc; }
    .empty { color: #666; }
    pre { background: #0b1020; color: #e6e6e6; padding: 12px; overflow: auto; border-radius: 6px; white-space: pre-wrap; }
    details { margin-top: 10px; }
    summary { cursor: pointer; font-weight: 600; }
    """
    parts = [
        "<!doctype html>",
        "<html><head><meta charset='utf-8'/>",
        "<title>SQL AI Advisor Report</title>",
        f"<style>{css}</style>",
        "</head><body>",
        "<h1>SQL AI Advisor Report</h1>",
        f"<div class='meta'>Generated at: {html_escape(time.strftime('%Y-%m-%d %H:%M:%S'))}</div>",
        "<h2>Query / Input</h2>",
        f"<pre>{html_escape(raw_query)}</pre>",
    ]

    for title, headers, rows in sections:
        # If this is a chart row, the cell contains raw SVG (we want it to render),
        # so we must NOT escape it.
        if headers == ["chart"]:
            parts.append(f"<h2>{html_escape(title)}</h2>")
            if not rows or not rows[0] or rows[0][0] is None:
                parts.append("<div class='empty'>(no data)</div>")
            else:
                parts.append(str(rows[0][0]))
            continue

        parts.append(html_table(title, headers, rows))

    if plan_xml:
        parts.append("<h2>Execution Plan XML</h2>")
        parts.append("<details><summary>Show/Hide Plan XML</summary>")
        parts.append(f"<pre>{html_escape(plan_xml)}</pre>")
        parts.append("</details>")

    parts.append("</body></html>")

    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(parts))


# =========================
# XML/plan helpers
# =========================
def sanitize_plan_text(s: str) -> str:
    if not s:
        return s
    i = s.find("<")
    if i > 0:
        s = s[i:]
    return s.strip()


def _xml_root(xml_text: str) -> Optional[etree._Element]:
    if not xml_text:
        return None
    try:
        parser = etree.XMLParser(remove_blank_text=True, recover=True)
        return etree.fromstring(xml_text.encode("utf-8", errors="ignore"), parser=parser)
    except Exception:
        return None


def pretty_xml(xml_text: str) -> str:
    xml_text = sanitize_plan_text(xml_text or "")
    root = _xml_root(xml_text)
    if root is None:
        s = (xml_text or "").strip()
        if len(s) > 20000:
            return s[:20000] + "\n<!-- TRUNCATED -->"
        return s
    return etree.tostring(root, pretty_print=True, encoding="utf-8", xml_declaration=True).decode("utf-8")


def plan_operator_outline(plan_xml: Optional[str], max_ops: int = 60) -> List[List[Any]]:
    plan_xml = sanitize_plan_text(plan_xml or "")
    if not plan_xml:
        return []
    root = _xml_root(plan_xml)
    if root is None:
        return []
    rows = []
    relops = root.xpath("//*[local-name()='RelOp']")
    for ro in relops[:max_ops]:
        rows.append([ro.get("PhysicalOp"), ro.get("LogicalOp"), ro.get("EstimateRows")])
    return rows


def extract_plan_runtime_summary(plan_xml: Optional[str]) -> Dict[str, Any]:
    out = {"has_runtime": False, "total_cpu_ms": None, "total_elapsed_ms": None}
    plan_xml = sanitize_plan_text(plan_xml or "")
    if not plan_xml:
        return out
    root = _xml_root(plan_xml)
    if root is None:
        return out

    counters = root.xpath("//*[local-name()='RunTimeCountersPerThread']")
    if not counters:
        return out

    total_cpu = 0.0
    total_elapsed = 0.0
    have_cpu = False
    have_elapsed = False
    for c in counters:
        cpu = c.get("ActualCPUms")
        elapsed = c.get("ActualElapsedms")
        if cpu is not None:
            have_cpu = True
            try:
                total_cpu += float(cpu)
            except Exception:
                pass
        if elapsed is not None:
            have_elapsed = True
            try:
                total_elapsed += float(elapsed)
            except Exception:
                pass

    out["has_runtime"] = have_cpu or have_elapsed
    out["total_cpu_ms"] = round(total_cpu, 3) if have_cpu else None
    out["total_elapsed_ms"] = round(total_elapsed, 3) if have_elapsed else None
    return out


def extract_plan_warnings(plan_xml: Optional[str], max_items: int = 50) -> List[str]:
    plan_xml = sanitize_plan_text(plan_xml or "")
    if not plan_xml:
        return []
    root = _xml_root(plan_xml)
    if root is None:
        return []
    warnings: List[str] = []
    for w in root.xpath("//*[local-name()='Warnings']"):
        txt = etree.tostring(w, encoding="unicode")
        if txt:
            warnings.append(re.sub(r"\s+", " ", txt).strip())
    for pac in root.xpath("//*[local-name()='PlanAffectingConvert']"):
        expr = pac.get("Expression") or ""
        data_type = pac.get("ConvertIssue") or ""
        warnings.append(f"PlanAffectingConvert: {data_type} {expr}".strip())
    seen = set()
    out = []
    for s in warnings:
        k = s.lower()
        if k and k not in seen:
            seen.add(k)
            out.append(s)
    return out[:max_items]


def extract_referenced_objects_from_plan(plan_xml: str) -> List[Dict[str, str]]:
    plan_xml = sanitize_plan_text(plan_xml or "")
    if not plan_xml:
        return []
    root = _xml_root(plan_xml)
    if root is None:
        return []
    objs = []
    for obj in root.xpath("//*[local-name()='Object']"):
        db = (obj.get("Database") or "").strip("[]")
        schema = (obj.get("Schema") or "").strip("[]")
        table = (obj.get("Table") or "").strip("[]")
        index = (obj.get("Index") or "").strip("[]")
        if schema and table:
            objs.append({"database": db, "schema": schema, "table": table, "index": index})
    seen = set()
    out = []
    for o in objs:
        key = (o["database"], o["schema"], o["table"], o["index"])
        if key not in seen:
            seen.add(key)
            out.append(o)
    return out


# =========================
# Server info
# =========================
def fetch_server_info(cur: pyodbc.Cursor) -> Dict[str, Any]:
    row = cur.execute("SELECT @@SERVERNAME AS server_name, DB_NAME() AS database_name, @@VERSION AS version_string;").fetchone()
    return {"server_name": row.server_name, "database_name": row.database_name, "version_string": row.version_string}


# =========================
# Query helpers
# =========================
def clamp_sql(sql: str) -> str:
    sql = (sql or "").strip()
    if len(sql) > MAX_SQL_CHARS:
        return sql[:MAX_SQL_CHARS] + "\n-- [TRUNCATED]"
    return sql


def normalize_sql(sql: str) -> str:
    return sqlparse.format(sql, strip_comments=True, keyword_case="upper").strip()


def first_keyword(sql: str) -> str:
    parsed = sqlparse.parse(sql)
    if not parsed:
        return ""
    stmt = parsed[0]
    for tok in stmt.flatten():
        if tok.is_whitespace:
            continue
        if tok.ttype in sqlparse.tokens.Comment:
            continue
        val = tok.value.strip()
        if val:
            return val.upper()
    return ""


def classify_query(sql: str) -> Dict[str, Any]:
    n = normalize_sql(sql)
    kw = first_keyword(n)
    if kw in ("SELECT", "WITH"):
        kind = "SELECT"
    elif kw in ("INSERT", "UPDATE", "DELETE", "MERGE"):
        kind = kw
    elif kw in ("CREATE", "ALTER", "DROP", "TRUNCATE", "RENAME"):
        kind = "DDL"
    elif kw in ("EXEC", "EXECUTE"):
        kind = "EXEC"
    elif kw in ("DECLARE", "SET", "BEGIN"):
        kind = "BATCH"
    else:
        kind = kw or "UNKNOWN"
    return {"kind": kind, "first_keyword": kw}


def extract_tsql_variables(sql: str) -> List[str]:
    vars_found = set(re.findall(r"(?<!@)@\w+", sql))
    vars_found = {v for v in vars_found if not v.startswith("@@")}
    return sorted(vars_found)


def parse_declare_overrides(s: str) -> Dict[str, str]:
    out: Dict[str, str] = {}
    s = (s or "").strip()
    if not s:
        return out
    parts = [p.strip() for p in s.split(",") if p.strip()]
    for p in parts:
        if "=" not in p:
            continue
        k, v = p.split("=", 1)
        k = k.strip()
        if not k.startswith("@"):
            k = "@" + k
        out[k] = v.strip()
    return out


def guess_var_type(varname: str) -> str:
    v = varname.lower()
    if "time" in v or "date" in v or v.endswith("_start") or v.endswith("_end"):
        return "datetime2"
    if "count" in v or "rows" in v or "row_count" in v or "min_exec" in v:
        return "int"
    return "sql_variant"


def wrap_for_showplan(sql: str, overrides: Dict[str, str]) -> str:
    vars_ = extract_tsql_variables(sql)
    if not vars_:
        return sql
    decls = []
    for v in vars_:
        t = overrides.get(v) or guess_var_type(v)
        if re.search(rf"\bTOP\s*\(\s*{re.escape(v)}\s*\)", sql, flags=re.IGNORECASE):
            t = overrides.get(v) or "int"
        decls.append(f"DECLARE {v} {t} = NULL;")
    return "\n".join(decls) + "\n" + sql


def try_get_estimated_plan_xml(
    cur: pyodbc.Cursor, sql: str, declare_overrides: Dict[str, str]
) -> Tuple[Optional[str], Optional[str], str]:
    wrapped = wrap_for_showplan(sql, declare_overrides)
    try:
        cur.execute("SET SHOWPLAN_XML ON;")
        try:
            r = cur.execute(wrapped).fetchone()
            if not r:
                return None, "SHOWPLAN_XML returned no rows.", wrapped
            return str(r[0]), None, wrapped
        finally:
            cur.execute("SET SHOWPLAN_XML OFF;")
    except Exception as e:
        return None, f"Estimated plan not available: {e}", wrapped


# =========================
# Table analysis helpers
# =========================
def split_schema_table(name: str) -> Tuple[str, str]:
    name = (name or "").strip()
    if "." in name:
        schema, table = name.split(".", 1)
        return schema.strip("[] "), table.strip("[] ")
    return "dbo", name.strip("[] ")


def read_table_list(table_args: List[str], table_file: str) -> List[str]:
    tables: List[str] = []
    for t in (table_args or []):
        t = (t or "").strip()
        if t:
            tables.append(t)
    if table_file:
        with open(table_file, "r", encoding="utf-8") as f:
            for line in f:
                t = line.strip()
                if t and not t.startswith("#"):
                    tables.append(t)
    seen = set()
    out: List[str] = []
    for t in tables:
        k = t.lower()
        if k not in seen:
            seen.add(k)
            out.append(t)
    return out


def table_exists(cur: pyodbc.Cursor, schema: str, table: str) -> bool:
    q = """
    SELECT 1
    FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = ? AND t.name = ?;
    """
    return cur.execute(q, (schema, table)).fetchone() is not None


def get_table_size(cur: pyodbc.Cursor, schema: str, table: str) -> Dict[str, Any]:
    q = """
    DECLARE @obj_id INT = OBJECT_ID(QUOTENAME(?) + '.' + QUOTENAME(?));
    SELECT
      SUM(ps.row_count) AS row_count,
      CAST(SUM(au.total_pages) * 8.0 / 1024 AS DECIMAL(18,2)) AS total_mb,
      CAST(SUM(au.used_pages) * 8.0 / 1024 AS DECIMAL(18,2)) AS used_mb,
      CAST((SUM(au.total_pages) - SUM(au.used_pages)) * 8.0 / 1024 AS DECIMAL(18,2)) AS unused_mb
    FROM sys.dm_db_partition_stats ps
    JOIN sys.partitions p
      ON p.object_id = ps.object_id AND p.index_id = ps.index_id AND p.partition_id = ps.partition_id
    JOIN sys.allocation_units au
      ON au.container_id = p.hobt_id
    WHERE ps.object_id = @obj_id;
    """
    r = cur.execute(q, (schema, table)).fetchone()
    if not r:
        return {"row_count": None, "total_mb": None, "used_mb": None, "unused_mb": None}
    return {
        "row_count": int(r.row_count) if r.row_count is not None else None,
        "total_mb": float(r.total_mb) if r.total_mb is not None else None,
        "used_mb": float(r.used_mb) if r.used_mb is not None else None,
        "unused_mb": float(r.unused_mb) if r.unused_mb is not None else None,
    }


def get_index_usage(cur: pyodbc.Cursor, schema: str, table: str) -> List[Dict[str, Any]]:
    q = """
    DECLARE @obj_id INT = OBJECT_ID(QUOTENAME(?) + '.' + QUOTENAME(?));
    SELECT
      i.name AS index_name,
      i.index_id,
      i.is_unique,
      i.is_primary_key,
      i.is_disabled,
      COALESCE(us.user_seeks, 0) AS user_seeks,
      COALESCE(us.user_scans, 0) AS user_scans,
      COALESCE(us.user_lookups, 0) AS user_lookups,
      COALESCE(us.user_updates, 0) AS user_updates,
      us.last_user_seek
    FROM sys.indexes i
    LEFT JOIN sys.dm_db_index_usage_stats us
      ON us.database_id = DB_ID()
     AND us.object_id = i.object_id
     AND us.index_id = i.index_id
    WHERE i.object_id = @obj_id AND i.index_id > 0
    ORDER BY (COALESCE(us.user_seeks,0) + COALESCE(us.user_scans,0) + COALESCE(us.user_lookups,0)) DESC, i.index_id;
    """
    rows = cur.execute(q, (schema, table)).fetchall()
    out = []
    for r in rows:
        out.append(
            {
                "index_name": r.index_name,
                "index_id": int(r.index_id),
                "is_unique": bool(r.is_unique),
                "is_primary_key": bool(r.is_primary_key),
                "is_disabled": bool(r.is_disabled),
                "user_seeks": int(r.user_seeks),
                "user_scans": int(r.user_scans),
                "user_lookups": int(r.user_lookups),
                "user_updates": int(r.user_updates),
                "last_user_seek": str(r.last_user_seek) if r.last_user_seek else None,
            }
        )
    return out


def get_table_statistics_with_columns(cur: pyodbc.Cursor, schema: str, table: str) -> List[Dict[str, Any]]:
    q = """
    DECLARE @obj_id INT = OBJECT_ID(QUOTENAME(?) + '.' + QUOTENAME(?));

    SELECT
      STUFF((
        SELECT ', ' + c.name
        FROM sys.stats_columns sc
        JOIN sys.columns c
          ON c.object_id = sc.object_id AND c.column_id = sc.column_id
        WHERE sc.object_id = s.object_id AND sc.stats_id = s.stats_id
        ORDER BY sc.stats_column_id
        FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)')
      , 1, 2, '') AS stats_columns,
      sp.last_updated,
      sp.rows,
      sp.modification_counter
    FROM sys.stats s
    OUTER APPLY sys.dm_db_stats_properties(s.object_id, s.stats_id) sp
    WHERE s.object_id = @obj_id
    ORDER BY sp.last_updated DESC;
    """
    rows = cur.execute(q, (schema, table)).fetchall()
    out = []
    for r in rows:
        out.append(
            {
                "stats_columns": r.stats_columns,
                "last_updated": str(r.last_updated) if r.last_updated else None,
                "rows": int(r.rows) if r.rows is not None else None,
                "modification_counter": int(r.modification_counter) if r.modification_counter is not None else None,
            }
        )
    return out


def get_table_fragmentation(cur: pyodbc.Cursor, schema: str, table: str) -> List[Dict[str, Any]]:
    q = """
    DECLARE @obj_id INT = OBJECT_ID(QUOTENAME(?) + '.' + QUOTENAME(?));
    SELECT i.name AS index_name, ips.avg_fragmentation_in_percent, ips.page_count
    FROM sys.dm_db_index_physical_stats(DB_ID(), @obj_id, NULL, NULL, 'LIMITED') ips
    JOIN sys.indexes i
      ON ips.object_id = i.object_id AND ips.index_id = i.index_id
    WHERE ips.index_id > 0
    ORDER BY ips.page_count DESC;
    """
    rows = cur.execute(q, (schema, table)).fetchall()
    out = []
    for r in rows:
        out.append(
            {
                "index_name": r.index_name,
                "avg_fragmentation_in_percent": float(r.avg_fragmentation_in_percent)
                if r.avg_fragmentation_in_percent is not None
                else None,
                "page_count": int(r.page_count) if r.page_count is not None else None,
            }
        )
    return out


def analyze_tables(cur: pyodbc.Cursor, tables: List[str]) -> Dict[str, Any]:
    result: Dict[str, Any] = {"tables": []}
    for t in tables:
        schema, table = split_schema_table(t)
        full_name = f"{schema}.{table}"
        if not table or not table_exists(cur, schema, table):
            result["tables"].append({"table": full_name, "error": "Table not found"})
            continue
        entry: Dict[str, Any] = {"table": full_name}
        entry["size"] = get_table_size(cur, schema, table)
        entry["statistics"] = get_table_statistics_with_columns(cur, schema, table)
        entry["fragmentation"] = get_table_fragmentation(cur, schema, table)
        entry["index_usage"] = get_index_usage(cur, schema, table)
        result["tables"].append(entry)
    return result


# =========================
# DB health checks
# =========================
def db_top_fragmented_indexes(cur: pyodbc.Cursor, top_n: int = 25) -> List[List[Any]]:
    q = f"""
    ;WITH ips AS (
        SELECT
            s.name AS schema_name,
            o.name AS table_name,
            i.name AS index_name,
            ips.avg_fragmentation_in_percent AS frag_pct,
            ips.page_count
        FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
        JOIN sys.indexes i
          ON i.object_id = ips.object_id AND i.index_id = ips.index_id
        JOIN sys.objects o
          ON o.object_id = ips.object_id
        JOIN sys.schemas s
          ON s.schema_id = o.schema_id
        WHERE o.type = 'U'
          AND ips.index_id > 0
          AND i.name IS NOT NULL
    )
    SELECT TOP ({int(top_n)})
        schema_name,
        table_name,
        index_name,
        CAST(frag_pct AS DECIMAL(10,2)) AS frag_pct,
        page_count,
        CASE
          WHEN page_count < 1000 THEN 'SKIP (small)'
          WHEN frag_pct >= 30 THEN 'REBUILD'
          WHEN frag_pct >= 10 THEN 'REORGANIZE'
          ELSE 'OK'
        END AS suggestion
    FROM ips
    ORDER BY frag_pct DESC, page_count DESC;
    """
    rows = cur.execute(q).fetchall()
    return [[r.schema_name, r.table_name, r.index_name, float(r.frag_pct), int(r.page_count), r.suggestion] for r in rows]


def db_stats_health(cur: pyodbc.Cursor, top_n: int = 50) -> List[List[Any]]:
    q = f"""
    ;WITH st AS (
      SELECT
        s.name AS schema_name,
        o.name AS table_name,
        st.name AS stats_name,
        sp.last_updated,
        sp.rows,
        sp.modification_counter,
        CASE
          WHEN sp.rows IS NULL OR sp.rows = 0 THEN NULL
          ELSE CAST(1.0 * sp.modification_counter / NULLIF(sp.rows,0) AS DECIMAL(18,4))
        END AS mod_ratio
      FROM sys.stats st
      JOIN sys.objects o ON o.object_id = st.object_id
      JOIN sys.schemas s ON s.schema_id = o.schema_id
      OUTER APPLY sys.dm_db_stats_properties(st.object_id, st.stats_id) sp
      WHERE o.type = 'U'
    )
    SELECT TOP ({int(top_n)})
      schema_name, table_name, stats_name, last_updated, rows, modification_counter, mod_ratio
    FROM st
    ORDER BY
      CASE WHEN mod_ratio IS NULL THEN 0 ELSE mod_ratio END DESC,
      modification_counter DESC;
    """
    rows = cur.execute(q).fetchall()
    out = []
    for r in rows:
        out.append(
            [
                r.schema_name,
                r.table_name,
                r.stats_name,
                str(r.last_updated) if r.last_updated else None,
                int(r.rows) if r.rows is not None else None,
                int(r.modification_counter) if r.modification_counter is not None else None,
                float(r.mod_ratio) if r.mod_ratio is not None else None,
            ]
        )
    return out


def db_unused_indexes(cur: pyodbc.Cursor, top_n: int = 50) -> List[List[Any]]:
    q = f"""
    ;WITH idx AS (
      SELECT
        s.name AS schema_name,
        o.name AS table_name,
        i.name AS index_name,
        i.index_id,
        i.is_primary_key,
        i.is_unique,
        i.is_disabled,
        COALESCE(us.user_seeks,0) AS user_seeks,
        COALESCE(us.user_scans,0) AS user_scans,
        COALESCE(us.user_lookups,0) AS user_lookups,
        COALESCE(us.user_updates,0) AS user_updates
      FROM sys.indexes i
      JOIN sys.objects o ON o.object_id = i.object_id
      JOIN sys.schemas s ON s.schema_id = o.schema_id
      LEFT JOIN sys.dm_db_index_usage_stats us
        ON us.database_id = DB_ID()
       AND us.object_id = i.object_id
       AND us.index_id = i.index_id
      WHERE o.type = 'U'
        AND i.index_id > 0
        AND i.name IS NOT NULL
    )
    SELECT TOP ({int(top_n)})
      schema_name, table_name, index_name,
      user_seeks, user_scans, user_lookups, user_updates,
      CASE
        WHEN (user_seeks + user_scans + user_lookups) = 0 AND user_updates > 0 THEN 'CANDIDATE'
        ELSE 'OK'
      END AS status
    FROM idx
    WHERE is_primary_key = 0 AND is_unique = 0 AND is_disabled = 0
    ORDER BY user_updates DESC, (user_seeks + user_scans + user_lookups) ASC;
    """
    rows = cur.execute(q).fetchall()
    return [[r.schema_name, r.table_name, r.index_name, int(r.user_seeks), int(r.user_scans), int(r.user_lookups), int(r.user_updates), r.status] for r in rows]


def db_io_latency(cur: pyodbc.Cursor) -> List[List[Any]]:
    q = """
    SELECT
      DB_NAME(vfs.database_id) AS database_name,
      mf.physical_name,
      vfs.num_of_reads,
      vfs.num_of_writes,
      vfs.io_stall_read_ms,
      vfs.io_stall_write_ms,
      CASE WHEN vfs.num_of_reads = 0 THEN NULL ELSE CAST(1.0 * vfs.io_stall_read_ms / vfs.num_of_reads AS DECIMAL(18,2)) END AS avg_read_ms,
      CASE WHEN vfs.num_of_writes = 0 THEN NULL ELSE CAST(1.0 * vfs.io_stall_write_ms / vfs.num_of_writes AS DECIMAL(18,2)) END AS avg_write_ms
    FROM sys.dm_io_virtual_file_stats(NULL, NULL) vfs
    JOIN sys.master_files mf
      ON mf.database_id = vfs.database_id AND mf.file_id = vfs.file_id
    WHERE vfs.database_id = DB_ID()
    ORDER BY (vfs.io_stall_read_ms + vfs.io_stall_write_ms) DESC;
    """
    rows = cur.execute(q).fetchall()
    out = []
    for r in rows:
        avg_r = float(r.avg_read_ms) if r.avg_read_ms is not None else None
        avg_w = float(r.avg_write_ms) if r.avg_write_ms is not None else None
        rec = "OK"
        if (avg_r is not None and avg_r > 20) or (avg_w is not None and avg_w > 20):
            rec = "High latency: review storage, tempdb placement, file growth, and I/O subsystem."
        out.append(
            [
                r.database_name,
                r.physical_name,
                int(r.num_of_reads),
                int(r.num_of_writes),
                int(r.io_stall_read_ms),
                int(r.io_stall_write_ms),
                avg_r,
                avg_w,
                rec,
            ]
        )
    return out


def db_options_overview(cur: pyodbc.Cursor) -> List[List[Any]]:
    q = """
    SELECT
      d.name,
      d.state_desc,
      d.user_access_desc,
      d.recovery_model_desc,
      d.compatibility_level,
      d.page_verify_option_desc,
      d.is_auto_close_on,
      d.is_auto_shrink_on,
      d.is_read_only,
      d.is_encrypted,
      d.log_reuse_wait_desc
    FROM sys.databases d
    WHERE d.database_id = DB_ID();
    """
    r = cur.execute(q).fetchone()
    if not r:
        return []
    return [
        ["name", r.name],
        ["state", r.state_desc],
        ["user_access", r.user_access_desc],
        ["recovery_model", r.recovery_model_desc],
        ["compatibility_level", r.compatibility_level],
        ["page_verify", r.page_verify_option_desc],
        ["auto_close", bool(r.is_auto_close_on)],
        ["auto_shrink", bool(r.is_auto_shrink_on)],
        ["read_only", bool(r.is_read_only)],
        ["encrypted", bool(r.is_encrypted)],
        ["log_reuse_wait", r.log_reuse_wait_desc],
    ]


def db_file_growth_settings(cur: pyodbc.Cursor) -> List[List[Any]]:
    q = """
    SELECT
      DB_NAME() AS database_name,
      df.file_id,
      df.type_desc,
      df.name AS logical_name,
      df.physical_name,
      CAST(df.size * 8.0 / 1024 AS DECIMAL(18,2)) AS size_mb,
      df.is_percent_growth,
      df.growth,
      CASE
        WHEN df.is_percent_growth = 1 THEN CONCAT(df.growth, ' %')
        ELSE CONCAT(CAST(df.growth * 8.0 / 1024 AS DECIMAL(18,2)), ' MB')
      END AS growth_desc,
      df.max_size
    FROM sys.database_files df
    ORDER BY df.type_desc, df.file_id;
    """
    rows = cur.execute(q).fetchall()
    out = []
    for r in rows:
        out.append(
            [
                r.database_name,
                int(r.file_id),
                r.type_desc,
                r.logical_name,
                r.physical_name,
                float(r.size_mb),
                bool(r.is_percent_growth),
                int(r.growth),
                r.growth_desc,
                int(r.max_size),
            ]
        )
    return out


def tempdb_overview(cur: pyodbc.Cursor) -> List[List[Any]]:
    q = """
    SELECT
      DB_NAME(mf.database_id) AS db_name,
      mf.type_desc,
      COUNT(*) AS file_count,
      CAST(SUM(mf.size) * 8.0 / 1024 AS DECIMAL(18,2)) AS size_mb
    FROM sys.master_files mf
    WHERE mf.database_id = 2
    GROUP BY mf.database_id, mf.type_desc
    ORDER BY mf.type_desc;
    """
    rows, err = safe_query_rows(cur, q)
    if not rows:
        return [["error", err or "not available"]]
    out = []
    for r in rows:
        out.append([r.db_name, r.type_desc, int(r.file_count), float(r.size_mb)])
    return out


def top_waits(cur: pyodbc.Cursor, top_n: int = 25) -> List[List[Any]]:
    q = f"""
    SELECT TOP ({int(top_n)})
      wait_type,
      waiting_tasks_count,
      wait_time_ms,
      signal_wait_time_ms,
      CAST(100.0 * wait_time_ms / NULLIF(SUM(wait_time_ms) OVER(),0) AS DECIMAL(6,2)) AS pct
    FROM sys.dm_os_wait_stats
    WHERE wait_type NOT LIKE 'SLEEP%%'
      AND wait_type NOT IN ('BROKER_TASK_STOP','BROKER_EVENTHANDLER','BROKER_RECEIVE_WAITFOR','CLR_SEMAPHORE','LAZYWRITER_SLEEP',
                            'RESOURCE_QUEUE','SQLTRACE_BUFFER_FLUSH','XE_TIMER_EVENT','XE_DISPATCHER_WAIT','REQUEST_FOR_DEADLOCK_SEARCH',
                            'LOGMGR_QUEUE','CHECKPOINT_QUEUE','FT_IFTS_SCHEDULER_IDLE_WAIT','BROKER_TO_FLUSH','BROKER_TRANSMITTER',
                            'DIRTY_PAGE_POLL')
    ORDER BY wait_time_ms DESC;
    """
    rows, err = safe_query_rows(cur, q)
    if not rows:
        return [["error", err or "not available"]]
    out = []
    for r in rows:
        out.append([r.wait_type, int(r.waiting_tasks_count), int(r.wait_time_ms), int(r.signal_wait_time_ms), float(r.pct)])
    return out


def backup_recency(cur: pyodbc.Cursor) -> List[List[Any]]:
    q = """
    SELECT
      d.name AS db_name,
      MAX(CASE WHEN bs.type = 'D' THEN bs.backup_finish_date END) AS last_full,
      MAX(CASE WHEN bs.type = 'I' THEN bs.backup_finish_date END) AS last_diff,
      MAX(CASE WHEN bs.type = 'L' THEN bs.backup_finish_date END) AS last_log
    FROM sys.databases d
    LEFT JOIN msdb.dbo.backupset bs
      ON bs.database_name = d.name
    GROUP BY d.name
    ORDER BY d.name;
    """
    rows, err = safe_query_rows(cur, q)
    if not rows:
        return [["error", err or "msdb not accessible or insufficient permissions"]]
    out = []
    for r in rows:
        out.append([r.db_name, str(r.last_full) if r.last_full else None, str(r.last_diff) if r.last_diff else None, str(r.last_log) if r.last_log else None])
    return out


# =========================
# Option 5: DB usage scan (all vs single)
# =========================
def prompt_db_usage_scope() -> Tuple[str, Optional[str]]:
    while True:
        print("\nDB usage scan scope:")
        print("  1) All databases")
        print("  2) Single database")
        choice = input("Choose (1/2) [default 1]: ").strip()
        if not choice:
            return ("all", None)
        if choice == "1":
            return ("all", None)
        if choice == "2":
            db = input("Enter database name: ").strip()
            return ("one", db if db else None)
        print("Invalid choice. Please enter 1 or 2.")


def server_last_start_time(cur: pyodbc.Cursor) -> Optional[str]:
    q1 = "SELECT CONVERT(varchar(19), sqlserver_start_time, 120) AS start_time FROM sys.dm_os_sys_info;"
    row, _ = safe_query_one(cur, q1)
    if row is not None:
        return row[0]
    q2 = "SELECT CONVERT(varchar(19), create_date, 120) AS start_time FROM sys.databases WHERE name = 'tempdb';"
    row, _ = safe_query_one(cur, q2)
    if row is not None:
        return row[0]
    return None


def db_has_query_store_enabled(cur: pyodbc.Cursor) -> bool:
    q = "SELECT actual_state_desc FROM sys.database_query_store_options;"
    row, _ = safe_query_one(cur, q)
    if row is None:
        return False
    state = str(row[0] or "").upper()
    return state in ("READ_WRITE", "READ_ONLY")


def db_query_store_activity_last_days(cur: pyodbc.Cursor, days: int) -> Optional[bool]:
    if not db_has_query_store_enabled(cur):
        return None
    q = f"""
    SELECT TOP (1) 1
    FROM sys.query_store_runtime_stats rs
    WHERE rs.last_execution_time >= DATEADD(DAY, -{int(days)}, SYSUTCDATETIME());
    """
    rows, err = safe_query_rows(cur, q)
    if err is not None:
        return None
    return bool(rows)


def db_index_usage_last_activity(cur: pyodbc.Cursor) -> Dict[str, Optional[str]]:
    q = """
    SELECT
      MAX(last_user_seek)   AS last_user_seek,
      MAX(last_user_scan)   AS last_user_scan,
      MAX(last_user_lookup) AS last_user_lookup,
      MAX(last_user_update) AS last_user_update
    FROM sys.dm_db_index_usage_stats
    WHERE database_id = DB_ID();
    """
    row, _ = safe_query_one(cur, q)
    if row is None:
        return {"last_user_read": None, "last_user_write": None}

    reads = [
        getattr(row, "last_user_seek", None),
        getattr(row, "last_user_scan", None),
        getattr(row, "last_user_lookup", None),
    ]
    last_read = max([x for x in reads if x is not None], default=None)
    last_write = getattr(row, "last_user_update", None)

    def sdt(x: Any) -> Optional[str]:
        return str(x) if x is not None else None

    return {"last_user_read": sdt(last_read), "last_user_write": sdt(last_write)}


def db_current_connections(cur: pyodbc.Cursor) -> Optional[int]:
    q = """
    SELECT COUNT(*) AS cnt
    FROM sys.dm_exec_sessions s
    WHERE s.is_user_process = 1
      AND s.database_id = DB_ID();
    """
    row, _ = safe_query_one(cur, q)
    if row is None:
        return None
    try:
        return int(row[0])
    except Exception:
        return None


def server_db_usage_scan(cur: pyodbc.Cursor, days: int = 30) -> Tuple[List[List[Any]], List[List[Any]]]:
    dbs_q = """
    SELECT name, state_desc, is_read_only
    FROM sys.databases
    WHERE name NOT IN ('tempdb')
    ORDER BY name;
    """
    db_rows, err = safe_query_rows(cur, dbs_q)
    if not db_rows:
        return [], [["(error)", f"Could not enumerate databases: {err}"]]

    start_time = server_last_start_time(cur)

    summary: List[List[Any]] = []
    notes: List[List[Any]] = []

    for d in db_rows:
        db_name = d.name
        state_desc = d.state_desc
        is_read_only = bool(d.is_read_only)

        err_use = use_db(cur, db_name)
        if err_use is not None:
            summary.append([db_name, state_desc, is_read_only, "N/A", None, None, None, "Inconclusive", "LOW"])
            notes.append([db_name, f"Cannot USE database: {err_use}"])
            continue

        qs_act = db_query_store_activity_last_days(cur, days)
        idx_last = db_index_usage_last_activity(cur)
        cur_conn = db_current_connections(cur)

        qs_status = "Yes" if qs_act is True else ("No" if qs_act is False else "N/A")

        note_parts = []
        if start_time:
            note_parts.append(f"server_start_time={start_time}")

        if qs_act is True:
            unused_candidate = "No"
            confidence = "HIGH"
            note_parts.append("Query Store shows executions in window.")
        elif qs_act is False and (cur_conn == 0 or cur_conn is None):
            unused_candidate = "Yes"
            confidence = "MEDIUM"
            note_parts.append("Query Store enabled and shows NO executions in window; no/unknown current user connections.")
        else:
            unused_candidate = "Inconclusive"
            confidence = "LOW" if qs_act is None else "MEDIUM"
            if qs_act is None:
                note_parts.append("Query Store not available/disabled; cannot reliably prove last-N-days usage.")
            else:
                note_parts.append("Query Store shows no executions OR current connections nonzero; conservative => Inconclusive.")
            if cur_conn is not None and cur_conn > 0:
                note_parts.append("Has current user connections.")

        summary.append(
            [
                db_name,
                state_desc,
                is_read_only,
                qs_status,
                cur_conn,
                idx_last["last_user_read"],
                idx_last["last_user_write"],
                unused_candidate,
                confidence,
            ]
        )
        notes.append([db_name, "; ".join(note_parts)])

    return summary, notes


# =========================
# Option 6: Historical DB size trend (msdb backupfile, monthly)
# =========================
def prompt_single_db_name() -> str:
    while True:
        db = input("Enter database name: ").strip()
        if db:
            return db
        print("Please enter a database name.")


def prompt_history_months() -> int:
    while True:
        print("\nHistorical trend range:")
        print("  1) Last 6 months")
        print("  2) Last 12 months")
        print("  3) Last 24 months")
        choice = input("Choose (1/2/3) [default 2]: ").strip()
        if not choice:
            return 12
        if choice == "1":
            return 6
        if choice == "2":
            return 12
        if choice == "3":
            return 24
        print("Invalid choice. Please enter 1, 2, or 3.")


def msdb_db_size_trend_by_month(cur: pyodbc.Cursor, months: int = 12) -> Tuple[List[str], List[float], List[List[Any]], Optional[str]]:
    """
    Returns:
      - labels: ["YYYY-MM", ...]
      - avg_sizes_mb: plotted series (AvgSizeMB)
      - table_rows: [month, min_mb, max_mb, avg_mb, growth_mb]
      - note/error

    Uses msdb backupset + backupfile (data files only) for FULL backups.
    Monthly aggregation.
    """
    q = f"""
    DECLARE @endDate datetime = GETDATE();
    DECLARE @months smallint = {int(months)};

    ;WITH HIST AS
       (SELECT BS.database_name AS DatabaseName
              ,YEAR(BS.backup_start_date) * 100 + MONTH(BS.backup_start_date) AS YearMonth
              ,CONVERT(numeric(18, 1), MIN(BF.file_size / 1048576.0)) AS MinSizeMB
              ,CONVERT(numeric(18, 1), MAX(BF.file_size / 1048576.0)) AS MaxSizeMB
              ,CONVERT(numeric(18, 1), AVG(BF.file_size / 1048576.0)) AS AvgSizeMB
        FROM msdb.dbo.backupset as BS
        INNER JOIN msdb.dbo.backupfile AS BF
          ON BS.backup_set_id = BF.backup_set_id
        WHERE BS.database_name = DB_NAME()
          AND BS.type = 'D'
          AND BF.file_type = 'D'
          AND BS.backup_start_date BETWEEN DATEADD(mm, -@months, @endDate) AND @endDate
        GROUP BY BS.database_name, YEAR(BS.backup_start_date), MONTH(BS.backup_start_date))
    SELECT
          MAIN.DatabaseName,
          MAIN.YearMonth,
          MAIN.MinSizeMB,
          MAIN.MaxSizeMB,
          MAIN.AvgSizeMB,
          MAIN.AvgSizeMB
           - (SELECT TOP 1 SUB.AvgSizeMB
              FROM HIST AS SUB
              WHERE SUB.DatabaseName = MAIN.DatabaseName
                AND SUB.YearMonth < MAIN.YearMonth
              ORDER BY SUB.YearMonth DESC) AS GrowthMB
    FROM HIST AS MAIN
    ORDER BY MAIN.YearMonth;
    """
    rows, err = safe_query_rows(cur, q)
    if not rows:
        return [], [], [], err or "No msdb full-backup history available (or insufficient permissions)."

    labels: List[str] = []
    vals: List[float] = []
    table_rows: List[List[Any]] = []
    for r in rows:
        ym = int(r.YearMonth)
        label = f"{ym // 100:04d}-{ym % 100:02d}"
        labels.append(label)
        vals.append(float(r.AvgSizeMB))
        table_rows.append(
            [
                label,
                float(r.MinSizeMB),
                float(r.MaxSizeMB),
                float(r.AvgSizeMB),
                float(r.GrowthMB) if r.GrowthMB is not None else None,
            ]
        )

    note = "Monthly trend based on msdb backupfile.file_size (data files) from FULL backups."
    return labels, vals, table_rows, note


# =========================
# Option 7: Future forecast (local snapshots)
# =========================
def exe_dir() -> str:
    if getattr(sys, "frozen", False):
        return os.path.dirname(sys.executable)
    return os.path.dirname(os.path.abspath(__file__))


def history_path() -> str:
    return os.path.join(exe_dir(), HISTORY_FILE_NAME)


def append_history_snapshot(payload: Dict[str, Any]) -> None:
    try:
        with open(history_path(), "a", encoding="utf-8") as f:
            f.write(json.dumps(payload, ensure_ascii=False) + "\n")
    except Exception:
        pass


def load_history_snapshots() -> List[Dict[str, Any]]:
    path = history_path()
    if not os.path.exists(path):
        return []
    out: List[Dict[str, Any]] = []
    try:
        with open(path, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                try:
                    out.append(json.loads(line))
                except Exception:
                    continue
    except Exception:
        return []
    return out


def server_db_file_sizes_mb(cur: pyodbc.Cursor) -> Dict[str, Dict[str, float]]:
    q = """
    SELECT
      DB_NAME(mf.database_id) AS db_name,
      mf.type_desc,
      CAST(SUM(mf.size) * 8.0 / 1024 AS DECIMAL(18,2)) AS size_mb
    FROM sys.master_files mf
    GROUP BY mf.database_id, mf.type_desc
    ORDER BY DB_NAME(mf.database_id), mf.type_desc;
    """
    rows = cur.execute(q).fetchall()

    per_db: Dict[str, Dict[str, float]] = {}
    for r in rows:
        db = r.db_name
        per_db.setdefault(db, {"data_mb": 0.0, "log_mb": 0.0})
        if "LOG" in str(r.type_desc).upper():
            per_db[db]["log_mb"] += float(r.size_mb)
        else:
            per_db[db]["data_mb"] += float(r.size_mb)
    return per_db


def capture_db_size_snapshot(cur: pyodbc.Cursor, server_name: str) -> int:
    per_db = server_db_file_sizes_mb(cur)
    ts_local = time.strftime("%Y-%m-%d %H:%M:%S")
    written = 0
    for db, sz in per_db.items():
        append_history_snapshot(
            {
                "kind": "db_size_snapshot",
                "ts_local": ts_local,
                "server_name": server_name,
                "db_name": db,
                "data_size_mb": round(sz.get("data_mb", 0.0), 4),
                "log_size_mb": round(sz.get("log_mb", 0.0), 4),
            }
        )
        written += 1
    return written


def _date_only(ts_local: str) -> Optional[str]:
    if not ts_local or len(ts_local) < 10:
        return None
    return ts_local[:10]


def build_daily_series_from_snapshots(points: List[Dict[str, Any]], history_window_days: int) -> Dict[str, Dict[str, Any]]:
    by_db_day: Dict[str, Dict[str, Dict[str, Any]]] = {}
    for p in points:
        db = p.get("db_name")
        day = _date_only(p.get("ts_local", ""))
        if not db or not day:
            continue
        by_db_day.setdefault(db, {})
        by_db_day[db][day] = p

    out: Dict[str, Dict[str, Any]] = {}
    for db in by_db_day:
        all_days = sorted(by_db_day[db].keys())
        days = all_days[-history_window_days:] if history_window_days > 0 else all_days
        dates = days
        data_sizes = [float(by_db_day[db][d].get("data_size_mb") or 0.0) for d in dates]
        out[db] = {"dates": dates, "data_sizes": data_sizes, "all_day_count": len(all_days)}
    return out


def forecast_growth_auto_arima_daily(dates: List[str], sizes: List[float], horizon_days: int) -> Dict[str, Any]:
    import numpy as np  # type: ignore
    import pandas as pd  # type: ignore
    from pmdarima import auto_arima  # type: ignore

    s = pd.Series(sizes, index=pd.to_datetime(dates)).sort_index()
    s = s.asfreq("D").ffill()
    y = s.values.astype(float)

    model = auto_arima(
        y,
        seasonal=False,
        stepwise=True,
        error_action="ignore",
        suppress_warnings=True,
        max_p=3,
        max_q=3,
        max_d=2,
    )
    fc = model.predict(n_periods=int(horizon_days))
    fc = np.maximum(fc, y[-1])
    return {"order": tuple(model.order), "current": float(y[-1]), "forecast": [float(x) for x in fc]}


# =========================
# Menu + other prompts
# =========================
def prompt_choice() -> str:
    while True:
        print("\nWhat do you want to analyze?")
        print("  1) SQL query")
        print("  2) Table(s)")
        print("  3) Execution plan (.sqlplan or plan XML)")
        print("  4) Full database health check")
        print("  5) DB usage scan (last 30 days) [decommission helper]")
        print("  6) Historical DB size growth trend (single DB) [graphs]")
        print("  7) Future DB size growth forecast (next 1 or 2 years) [Auto-ARIMA + graphs]")
        choice = input("Enter choice (1/2/3/4/5/6/7): ").strip()
        if choice in ("1", "2", "3", "4", "5", "6", "7"):
            return choice
        print("Invalid choice. Please enter 1..7.")


def prompt_plan_input_method() -> str:
    while True:
        print("\nProvide execution plan as:")
        print("  1) File path (.sqlplan / .xml)")
        print("  2) Paste XML")
        choice = input("Enter choice (1/2): ").strip()
        if choice in ("1", "2"):
            return choice
        print("Invalid choice. Please enter 1 or 2.")


def prompt_yes_no(question: str, default: str = "y") -> bool:
    default = default.lower().strip()
    suffix = " [Y/n]: " if default == "y" else " [y/N]: "
    while True:
        ans = input(question + suffix).strip().lower()
        if not ans:
            ans = default
        if ans in ("y", "yes"):
            return True
        if ans in ("n", "no"):
            return False
        print("Please answer y or n.")


def prompt_future_years() -> int:
    while True:
        print("\nFuture forecast horizon:")
        print("  1) Next 1 year")
        print("  2) Next 2 years")
        choice = input("Choose (1/2) [default 2]: ").strip()
        if not choice:
            return 2
        if choice == "1":
            return 1
        if choice == "2":
            return 2
        print("Invalid choice. Please enter 1 or 2.")


def prompt_history_window_days() -> int:
    while True:
        print("\nLocal snapshot history window (for forecast):")
        print("  1) Last 30 days")
        print("  2) Last 60 days")
        print("  3) Last 90 days")
        choice = input("Choose (1/2/3) [default 1]: ").strip()
        if not choice:
            return 30
        if choice == "1":
            return 30
        if choice == "2":
            return 60
        if choice == "3":
            return 90
        print("Invalid choice. Please enter 1, 2, or 3.")


def read_sql_from_stdin() -> str:
    print("Paste T-SQL (end with a blank line):")
    lines = []
    while True:
        try:
            line = input()
        except EOFError:
            break
        if line.strip() == "":
            break
        lines.append(line)
    return "\n".join(lines).strip()


def prompt_table_names_multi() -> List[str]:
    print("Enter table name(s) (schema.table or table). You can enter multiple, separated by commas.")
    print("Press Enter on an empty line to finish.")
    items: List[str] = []
    while True:
        line = input("> ").strip()
        if not line:
            break
        for part in line.split(","):
            t = part.strip()
            if t:
                items.append(t)
    return read_table_list(items, "")


def read_plan_from_stdin() -> str:
    print("Paste execution plan XML (end with a blank line):")
    lines = []
    while True:
        try:
            line = input()
        except EOFError:
            break
        if line.strip() == "":
            break
        lines.append(line)
    return sanitize_plan_text("\n".join(lines).strip())


def read_plan_from_file(path: str) -> str:
    path = (path or "").strip().strip('"')
    if not path:
        raise ValueError("Plan file path is empty.")
    if not os.path.exists(path):
        raise FileNotFoundError(f"Plan file not found: {path}")
    if os.path.isdir(path):
        raise IsADirectoryError(f"You provided a folder path, not a file path: {path}")
    with open(path, "rb") as f:
        raw = f.read()
    text = raw.decode("utf-8", errors="ignore")
    text = sanitize_plan_text(text)
    if not text.lstrip().startswith("<"):
        raise ValueError("The provided plan file does not look like XML text.")
    return text


# =========================
# AI helper
# =========================
def build_llm_payload(
    mode: str,
    query: str,
    server_info: Dict[str, Any],
    plan_err: Optional[str],
    referenced_tables: List[str],
    plan_summary: Dict[str, Any],
    plan_warnings: List[str],
) -> Dict[str, Any]:
    return {
        "mode": mode,
        "query": query,
        "server_info": server_info,
        "estimated_plan_error": plan_err,
        "referenced_tables": referenced_tables,
        "plan_summary": plan_summary,
        "plan_warnings": plan_warnings,
        "rules": {"do_not_suggest_sys_indexes": True},
    }


def build_prompt(payload: Dict[str, Any], lang: str) -> str:
    lang_line = "Write in English." if lang.lower().startswith("en") else "Write in the user's language."
    return f"""
You are a senior Microsoft SQL Server performance engineer. {lang_line}

Return STRICT JSON only with keys:
"summary","query_type","top_issues","recommendations","rewrite_suggestion","index_suggestions","safety_warnings","questions"

Input JSON:
{json.dumps(payload, ensure_ascii=False)}
""".strip()


def call_ollama_json(prompt: str, num_predict: int, debug: bool) -> Dict[str, Any]:
    url = f"{OLLAMA_URL}/api/chat"
    req = {
        "model": OLLAMA_MODEL,
        "stream": False,
        "format": "json",
        "messages": [
            {"role": "system", "content": "You are an expert SQL Server performance assistant."},
            {"role": "user", "content": prompt},
        ],
        "options": {"temperature": 0.2, "num_predict": num_predict},
    }
    r = requests.post(url, json=req, timeout=240)
    if r.status_code != 200:
        raise RuntimeError(f"Ollama error {r.status_code}: {r.text}")
    data = r.json()
    content = ((data.get("message") or {}).get("content")) or ""
    if debug:
        print("\n--- Raw Ollama response (first 1200 chars) ---")
        print(content[:1200])
        print("--- end raw ---\n")
    return json.loads(content)


# =========================
# Main
# =========================
def now_stamp() -> str:
    return time.strftime("%Y%m%d_%H%M%S")


def no_user_flags(argv: List[str]) -> bool:
    return len(argv) <= 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--lang", default="en")
    ap.add_argument("--predict", type=int, default=2500)
    ap.add_argument("--debug", action="store_true")
    ap.add_argument("--plan-out", default="", help="Write estimated plan XML to file (query mode)")
    ap.add_argument("--html-out", default="", help="Write HTML report")
    ap.add_argument("--out", default="", help="Write JSON report")
    ap.add_argument("--declare", default="", help="Override variable types, e.g. '@x=int,@y=datetime2'")
    ap.add_argument("--table", action="append", default=[])
    ap.add_argument("--table-file", default="")
    ap.add_argument("--plan-file", default="")
    ap.add_argument("--db-health", action="store_true")
    args = ap.parse_args()

    novice_mode = no_user_flags(sys.argv)
    declare_overrides = parse_declare_overrides(args.declare)
    tables_to_analyze = read_table_list(args.table, args.table_file)

    if args.db_health:
        mode = "db_health"
    elif args.plan_file:
        mode = "plan"
    elif tables_to_analyze:
        mode = "table"
    else:
        if not sys.stdin.isatty():
            mode = "query"
        else:
            c = prompt_choice()
            mode = {"1": "query", "2": "table", "3": "plan", "4": "db_health", "5": "db_usage", "6": "trend", "7": "forecast"}[c]

    if novice_mode and not args.html_out:
        if prompt_yes_no("Do you want to save an HTML report?", default="y"):
            args.html_out = f"sql_ai_report_{now_stamp()}.html"
            print(f"HTML report will be saved to: {args.html_out}")

    plan_xml: Optional[str] = None
    plan_err: Optional[str] = None
    raw_input_for_report = ""
    query_text_for_ai = ""

    if mode == "query":
        sql_input = clamp_sql(read_sql_from_stdin())
        if not sql_input:
            print("No SQL provided.")
            return
        raw_input_for_report = sql_input
        query_text_for_ai = sql_input
    elif mode == "table":
        if not tables_to_analyze:
            tables_to_analyze = prompt_table_names_multi()
        if not tables_to_analyze:
            print("No tables provided.")
            return
        raw_input_for_report = "TABLE MODE INPUT:\n" + "\n".join(tables_to_analyze)
        query_text_for_ai = raw_input_for_report
    elif mode == "plan":
        if args.plan_file:
            plan_xml = read_plan_from_file(args.plan_file)
            raw_input_for_report = f"PLAN FILE: {args.plan_file}"
        else:
            if not sys.stdin.isatty():
                plan_xml = sanitize_plan_text(sys.stdin.read())
                raw_input_for_report = "PLAN XML (piped via stdin)"
            else:
                how = prompt_plan_input_method()
                if how == "1":
                    path = input("Enter plan file path: ").strip().strip('"')
                    plan_xml = read_plan_from_file(path)
                    raw_input_for_report = f"PLAN FILE: {path}"
                else:
                    plan_xml = read_plan_from_stdin()
                    raw_input_for_report = "PLAN XML (pasted)"
        query_text_for_ai = raw_input_for_report
    else:
        raw_input_for_report = mode.upper()
        query_text_for_ai = raw_input_for_report

    run_summary = [
        ["mode", mode],
        ["ollama_model", OLLAMA_MODEL],
        ["html_out", args.html_out or "(none)"],
        ["history_file", history_path()],
        ["forecast_min_distinct_days", str(FORECAST_MIN_DISTINCT_DAYS)],
    ]
    if mode == "query":
        run_summary.insert(1, ["query_type_detected", classify_query(query_text_for_ai).get("kind")])

    print_table("Run summary", ["field", "value"], run_summary, max_width=110)

    print("\nCollecting SQL Server diagnostics...")
    start = time.time()

    sections: List[Tuple[str, List[str], List[List[Any]]]] = []
    plan_outline_rows: List[List[Any]] = []
    plan_runtime = {"has_runtime": False, "total_cpu_ms": None, "total_elapsed_ms": None}
    plan_warnings: List[str] = []
    referenced_tables: List[str] = []
    server_info: Dict[str, Any] = {}

    with connect() as conn:
        cur = conn.cursor()
        server_info = fetch_server_info(cur)
        server_name = str(server_info.get("server_name") or "")

        if mode == "query":
            sql_input = query_text_for_ai
            plan_xml, plan_err, _wrapped = try_get_estimated_plan_xml(cur, sql_input, declare_overrides)
            if plan_xml and args.plan_out:
                with open(args.plan_out, "w", encoding="utf-8") as f:
                    f.write(pretty_xml(plan_xml))

            plan_outline_rows = plan_operator_outline(plan_xml, max_ops=60)
            plan_runtime = extract_plan_runtime_summary(plan_xml)
            plan_warnings = extract_plan_warnings(plan_xml)

            referenced = extract_referenced_objects_from_plan(plan_xml or "")
            referenced_tables = sorted({f"{o.get('schema')}.{o.get('table')}" for o in referenced if o.get("schema") and o.get("table")})

            sections.append(("Plan status", ["field", "value"], [["error", plan_err]] if plan_err else [["status", "OK"]]))
            sections.append(("Plan runtime summary", ["field", "value"], [["has_runtime", plan_runtime["has_runtime"]], ["total_cpu_ms", plan_runtime["total_cpu_ms"]], ["total_elapsed_ms", plan_runtime["total_elapsed_ms"]]]))
            sections.append(("Plan operator outline (first 60)", ["physical_op", "logical_op", "estimate_rows"], plan_outline_rows))
            sections.append(("Plan warnings", ["warning"], [[w] for w in plan_warnings]))
            sections.append(("Referenced tables (from plan)", ["table"], [[t] for t in referenced_tables]))

        elif mode == "table":
            db_state = analyze_tables(cur, tables_to_analyze)
            for t in db_state.get("tables") or []:
                table = t.get("table")
                if t.get("error"):
                    sections.append((f"Table analysis for {table}", ["field", "value"], [["error", t.get("error")]]))
                    continue
                sz = t.get("size") or {}
                sections.append((f"Table size for {table}", ["metric", "value"], [["row_count", sz.get("row_count")], ["total_mb", sz.get("total_mb")], ["used_mb", sz.get("used_mb")], ["unused_mb", sz.get("unused_mb")]]))
                iu = t.get("index_usage") or []
                iu_rows = [[x.get("index_name"), x.get("user_seeks"), x.get("user_scans"), x.get("user_lookups"), x.get("user_updates"), x.get("last_user_seek")] for x in iu]
                sections.append((f"Index usage for {table}", ["index", "seeks", "scans", "lookups", "updates", "last_seek"], iu_rows))
                st = t.get("statistics") or []
                st_rows = [[x.get("stats_columns"), x.get("last_updated"), x.get("rows"), x.get("modification_counter")] for x in st]
                sections.append((f"Statistics for {table}", ["stats_columns", "last_updated", "rows", "modification_counter"], st_rows))
                fr = t.get("fragmentation") or []
                fr_rows = [[x.get("index_name"), x.get("avg_fragmentation_in_percent"), x.get("page_count")] for x in fr]
                sections.append((f"Fragmentation for {table}", ["index", "frag_%", "page_count"], fr_rows))

        elif mode == "plan":
            plan_outline_rows = plan_operator_outline(plan_xml, max_ops=60)
            plan_runtime = extract_plan_runtime_summary(plan_xml)
            plan_warnings = extract_plan_warnings(plan_xml)
            referenced = extract_referenced_objects_from_plan(plan_xml or "")
            referenced_tables = sorted({f"{o.get('schema')}.{o.get('table')}" for o in referenced if o.get("schema") and o.get("table")})
            sections.append(("Plan runtime summary", ["field", "value"], [["has_runtime", plan_runtime["has_runtime"]], ["total_cpu_ms", plan_runtime["total_cpu_ms"]], ["total_elapsed_ms", plan_runtime["total_elapsed_ms"]]]))
            sections.append(("Plan operator outline (first 60)", ["physical_op", "logical_op", "estimate_rows"], plan_outline_rows))
            sections.append(("Plan warnings", ["warning"], [[w] for w in plan_warnings]))
            sections.append(("Referenced tables (from plan)", ["table"], [[t] for t in referenced_tables]))

        elif mode == "db_health":
            sections.append(("DB options (overview)", ["field", "value"], db_options_overview(cur)))
            sections.append(("DB file growth settings", ["db", "file_id", "type", "logical", "physical", "size_mb", "pct_growth", "growth_raw", "growth_desc", "max_size"], db_file_growth_settings(cur)))
            sections.append(("DB Health: Top fragmented indexes", ["schema", "table", "index", "frag_pct", "page_count", "suggestion"], db_top_fragmented_indexes(cur, top_n=25)))
            sections.append(("DB Health: Statistics health", ["schema", "table", "stats", "last_updated", "rows", "mod_counter", "mod_ratio"], db_stats_health(cur, top_n=50)))
            sections.append(("DB Health: Unused indexes", ["schema", "table", "index", "seeks", "scans", "lookups", "updates", "status"], db_unused_indexes(cur, top_n=50)))
            sections.append(("DB Health: I/O latency", ["db", "physical", "reads", "writes", "stall_read_ms", "stall_write_ms", "avg_read_ms", "avg_write_ms", "recommendation"], db_io_latency(cur)))
            sections.append(("Server: Top waits", ["wait_type", "tasks", "wait_ms", "signal_ms", "pct"], top_waits(cur, top_n=25)))
            sections.append(("Server: TempDB overview", ["db", "type", "file_count", "size_mb"], tempdb_overview(cur)))
            sections.append(("Server: Backup recency (msdb)", ["db", "last_full", "last_diff", "last_log"], backup_recency(cur)))

        elif mode == "db_usage":
            days = 30
            scope, one_db = prompt_db_usage_scope()
            usage_summary, usage_notes = server_db_usage_scan(cur, days=days)

            if scope == "one" and one_db:
                usage_summary = [r for r in usage_summary if str(r[0]).lower() == one_db.lower()]
                usage_notes = [r for r in usage_notes if str(r[0]).lower() == one_db.lower()]

            print_table_truncated(
                f"DB Usage summary ({'single DB' if (scope=='one' and one_db) else 'all DBs'}, last {days} days) - conservative",
                ["db_name", "state", "read_only", "qs", "conn", "last_read", "last_write", "unused", "confidence"],
                usage_summary,
                col_max=[24, 10, 8, 4, 4, 19, 19, 12, 10],
            )
            print_table("DB Usage notes", ["db_name", "notes"], usage_notes, max_width=120)
            sections.append((f"DB Usage summary ({'single DB' if (scope=='one' and one_db) else 'all DBs'}, last {days} days) - conservative", ["db_name", "state", "read_only", "qs", "conn", "last_read", "last_write", "unused", "confidence"], usage_summary))
            sections.append(("DB Usage notes", ["db_name", "notes"], usage_notes))

        elif mode == "trend":
            months = prompt_history_months()
            db = prompt_single_db_name()

            err_use = use_db(cur, db)
            if err_use is not None:
                sections.append(("Historical trend", ["field", "value"], [["error", f"Cannot USE [{db}]: {err_use}"]]))
            else:
                labels, avg_mb, table_rows, note = msdb_db_size_trend_by_month(cur, months=months)
                if len(avg_mb) < 2:
                    sections.append(("Historical trend", ["field", "value"], [["db", db], ["note", note or "Not enough points to plot."]]))
                else:
                    print(f"\nHistorical DB size trend for {db} (monthly AvgSizeMB, last {months} month(s))")
                    print(ascii_sparkline(avg_mb, width=90))
                    print(f"  from={labels[0]}  to={labels[-1]}  last_avg_mb={avg_mb[-1]:.1f}")

                    # HTML/JSON
                    sections.append((f"Historical size trend graph for {db} (AvgSizeMB)", ["chart"], [[svg_line_chart(avg_mb)]]))
                    sections.append((f"Historical size trend data for {db}", ["month", "min_mb", "max_mb", "avg_mb", "growth_mb"], table_rows))
                    if note:
                        sections.append(("Historical trend note", ["note"], [[note]]))

        elif mode == "forecast":
            years = prompt_future_years()
            hist_window = prompt_history_window_days()
            horizon_days = years * FORECAST_DAYS_PER_YEAR

            written = capture_db_size_snapshot(cur, server_name)
            sections.append(("History capture (local snapshots)", ["field", "value"], [["snapshots_written", written], ["history_file", history_path()]]))

            hist = load_history_snapshots()
            points = [h for h in hist if h.get("kind") == "db_size_snapshot" and h.get("server_name") == server_name]
            series_by_db = build_daily_series_from_snapshots(points, history_window_days=hist_window)

            notes: List[List[Any]] = []
            summary_rows: List[List[Any]] = []

            for db in sorted(series_by_db.keys(), key=lambda x: x.lower()):
                s = series_by_db[db]
                if len(s["dates"]) < FORECAST_MIN_DISTINCT_DAYS:
                    notes.append([db, f"Not enough distinct days in local snapshot history window: have {len(s['dates'])}, need {FORECAST_MIN_DISTINCT_DAYS}."])
                    continue
                try:
                    fc = forecast_growth_auto_arima_daily(s["dates"], s["data_sizes"], horizon_days=horizon_days)
                    end_val = float(fc["forecast"][-1])
                    summary_rows.append([db, round(float(fc["current"]), 2), years, round(end_val, 2), str(fc.get("order"))])

                    combined = s["data_sizes"] + fc["forecast"]
                    divider = len(s["data_sizes"]) - 1
                    print(f"\nForecast graph (MB) for {db} | history {len(s['data_sizes'])}d + forecast {years}y")
                    print(ascii_sparkline(combined, width=90))
                    print(f"  current={fc['current']:.2f}MB  forecast_end={end_val:.2f}MB")

                    sections.append((f"Forecast graph for {db} (history + next {years}y, data size MB)", ["chart"], [[svg_line_chart(combined, divider_at_index=divider)]]))
                except Exception as e:
                    notes.append([db, f"Auto-ARIMA failed: {type(e).__name__}: {e} (install numpy/pandas/pmdarima)."])

            sections.append((f"Future DB size growth forecast (local snapshots, next {years} year(s))", ["db", "current_mb", "years", "forecast_end_mb", "model_order"], summary_rows))
            sections.append(("Forecast notes", ["db", "note"], notes))

        # Print sections to console: DO NOT print SVG markup
        for title, headers, rows in sections:
            if headers == ["chart"]:
                continue
            print_table(title, headers, rows, max_width=120)

        # AI call (optional)
        try:
            print("\nCalling local LLM (Ollama) ...\n")
            plan_summary = {"runtime": plan_runtime, "operator_outline_first_60": plan_outline_rows[:60]} if plan_outline_rows else {}
            ai_payload = build_llm_payload(
                mode=mode,
                query=query_text_for_ai,
                server_info=server_info,
                plan_err=plan_err,
                referenced_tables=referenced_tables,
                plan_summary=plan_summary,
                plan_warnings=plan_warnings,
            )
            prompt = build_prompt(ai_payload, args.lang)
            ai = call_ollama_json(prompt, num_predict=args.predict, debug=args.debug)
            print_table("AI summary", ["field", "value"], [["query_type", str(ai.get("query_type") or "")], ["summary", str(ai.get("summary") or "")]], max_width=120)
        except Exception as e:
            print_table("AI status", ["field", "value"], [["status", "SKIPPED"], ["reason", str(e)]], max_width=120)

    print(f"\nDiagnostics collected in {time.time() - start:.2f}s.")

    if args.html_out:
        full_sections = [("Run summary", ["field", "value"], run_summary)] + sections
        write_html_report(args.html_out, full_sections, raw_query=raw_input_for_report, plan_xml=pretty_xml(plan_xml) if plan_xml else None)
        print(f"Wrote HTML report to: {args.html_out}")

    if args.out:
        bundle = {
            "mode": mode,
            "run_summary": run_summary,
            "server_info": server_info,
            "history_file": history_path(),
            "sections": [{"title": t, "headers": h, "rows": r} for (t, h, r) in sections],
        }
        with open(args.out, "w", encoding="utf-8") as f:
            json.dump(bundle, f, indent=2, ensure_ascii=False)
        print(f"Wrote JSON report to: {args.out}")


if __name__ == "__main__":
    main()