import sys
import sqlite3
import matplotlib.pyplot as plt
import pandas as pd

# Load historical data

# Connect to SQL Server database
conn = sqlite3.connect('sql_advisor.db')
cursor = conn.cursor()

# Create the history table if it doesn't exist
def create_history_table():
    cursor.execute("CREATE TABLE IF NOT EXISTS dbo.SqlAiAdvisor_DbSizeHistory (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        db_size INTEGER NOT NULL,
        recorded_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
    )")
    conn.commit()

# Function to record the current database size

def record_current_db_size():
    cursor.execute("SELECT SUM(size) FROM sys.master_files")
    current_size = cursor.fetchone()[0]
    cursor.execute("INSERT INTO dbo.SqlAiAdvisor_DbSizeHistory (db_size) VALUES (?)", (current_size,))
    conn.commit()  

# Load historical data

# Option 6: Daily History Trend

def option_6():
    query = "SELECT recorded_at, db_size FROM dbo.SqlAiAdvisor_DbSizeHistory ORDER BY recorded_at"
    df = pd.read_sql_query(query, conn)
    plt.plot(df['recorded_at'], df['db_size'])
    plt.title('Daily DB Size Trend')
    plt.xlabel('Date')
    plt.ylabel('Database Size')
    plt.xticks(rotation=45)
    plt.tight_layout()
    plt.show()

# Option 7: Forecast

def option_7():
    # Capture a snapshot for the next year
    forecast_period = 365
    # Forecast logic here, using db size data

# Menu

create_history_table()  
record_current_db_size()  

while True:
    print("Choose an option:")
    print("0) Exit")
    print("1) Option 1")
    print("2) Option 2")
    print("3) Option 3")
    print("4) Option 4")
    print("5) Option 5")
    print("6) Daily DB Size Trend")
    print("7) Forecast")
    choice = input("Enter your choice: ")
    if choice == '0':
        print("Exiting...")
        break
    elif choice == '6':
        option_6()
    elif choice == '7':
        option_7()
    else:
        print("Invalid choice, please try again.")

# Close the connection
conn.close()