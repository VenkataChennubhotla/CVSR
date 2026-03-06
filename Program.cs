/******************************************************************************************
 * Important note about SSL Certificates: 
 *
 * If you are running your application from a different machine than where the RestAPI service is running,
 * you must setup an SSL certificate that is trusted on both machines. Servers and clients go through a handshake 
 * which includes the server sending the client its SSL certificate which the client then authenticates.  
 * In a production environment, RestApi should have an SSL Certificate issued by a certificate authority 
 * and clients can verify the certificate's digital signature using the public key of the certificate authority 
 * that issued it.
 * 
 * If you are running in a development environment see
 * https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-dev-certs
 * For info on how to create a development certificate that can be used.
 * 
 * If .net CLI cannot be used or is undesired, see Readme.MD for info on a method to create a self signed
 * certificate that can be used.
 * 
 * No matter which path is taken the Certificate needs to be set up in Appsettings.json Kestrel section.
*******************************************************************************************/

// The .NET Generic Host is a feature which sets up some convenient patterns for an application
// including those for dependency injection (DI), logging, and configuration
//https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host?tabs=appbuilder

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Log4Net.AspNetCore.Entities;
using CNSDemo;
using CNSDemo.AppOptions;

var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();

//this demo app has 2 potential switches: -l or --logpath for an alternative directory for the log file. and -d for debug level.
builder.Configuration.AddCommandLine(args, new Dictionary<string, string>() {
    { "-d", "d" },
    { "-l", "logpath" }
});
//the default location for the log file will be where the application .exe is located
var logfileName = @"CadNotificationServiceDemo%date{yyyyMMdd}.log";
var logfilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logfileName);

var clientLogPath = builder.Configuration["logpath"];
if (!string.IsNullOrWhiteSpace(clientLogPath))
{
    logfilePath = Path.Combine(clientLogPath, logfileName);
}

//Un-comment the line below if you want logging to be sent to the console
//builder.Logging.AddConsole();

//adding log4net for logging as well; there are several custom steps to make logging behave
//similarly to other OnCall Services
var log4netOptions = new Log4NetProviderOptions()
{
    PropertyOverrides = [
                            new NodeInfo()
                            {
                                XPath = "/log4net/appender[@name='Log']/file",
                                Attributes = new Dictionary<string, string>()
                                {
                                    { "value",  logfilePath }
                                }
                            }
                        ]
};

// The lines below will handle the command line switch -d value. Each debug level
// will print log lines of that value plus those above it. For instance setting
// the commandline to -d 3 means that all logger.Info(), logger.Warn(), and logger.Error() statements
// you see in the code will be printed in the logs. only logger.Debug() will be ignored in this case.
var debugLevel = builder.Configuration["d"];
if (int.TryParse(debugLevel, out int parsedLevel))
{
    var newLevel = parsedLevel switch
    {
        0 => "Off",
        1 => "Error",
        2 => "Warn",
        3 => "Info",
        >= 4 => "Debug",
        _ => throw new NotImplementedException(),
    };

    log4netOptions.PropertyOverrides.Add(new NodeInfo()
    {
        XPath = "/log4net/root/level",
        Attributes = new Dictionary<string, string>()
        {
            { "value", newLevel }
        }
    });

    builder.Logging.SetMinimumLevel(parsedLevel switch
    {
        0 => LogLevel.None,
        1 => LogLevel.Error,
        2 => LogLevel.Warning,
        3 => LogLevel.Information,
        >= 4 => LogLevel.Debug,
        _ => throw new NotImplementedException(),
    });
}
else
{
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}

builder.Logging.AddLog4Net(log4netOptions);


//Bind the custom configuration.
builder.Services
    .AddOptions<ApplicationOptions>()
    .Bind(builder.Configuration.GetSection(ApplicationOptions.ConfigurationSectionName));

//Add the auto generated validator.
builder.Services
    .AddSingleton<IValidateOptions<ApplicationOptions>, ValidateSettingsOptions>();

builder.Services.AddHttpClient(); //Dependency injection - Adds the IHttpFactory and related services to the IServiceCollection
builder.Services.AddSingleton(typeof(IRestfulCadApi), typeof(RestfulCadApi)); //needed for authorization
builder.Services.AddSingleton(typeof(ICadNotificationApi), typeof(CadNotificationApi)); //endpoints from CNS
builder.Services.AddSingleton(typeof(CadNotificationClient));

builder.Services.AddHostedService<CnsDemoService>(); //the main service - when host.StartAsync happens the StartAsync task in CnsDemoService will begin

using IHost host = builder.Build();

//this is for logging that will appear in this file
var logingFactory = host.Services.GetRequiredService<ILoggerFactory>();
var log = logingFactory.CreateLogger("Main");

try
{
    //Grabbing the options like this forces the validator to run.
    var options = host.Services.GetRequiredService<IOptions<ApplicationOptions>>().Value;
    var config = host.Services.GetRequiredService<IConfiguration>();
}
catch (OptionsValidationException e)
{
    log.LogError(e, "Appsettings.json appears to be invalid.");
    Environment.Exit(1);
}

//all the registered services will begin their StartAsync()
await host.RunAsync();
Environment.Exit(0);
