using CAP;
using Microsoft.OpenApi.Models;
using Redis.Service;
using Serilog;
using SignalR;
using FusionProgram.Extensions;
using CustomConfigExtensions;
using AgileConfig.Client;
using FusionProgram.Quartz;
using DapperSQL;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// ���AgileConfig
builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
    IConfigurationRoot configRoot = config.Build();
    if (configRoot.GetSection("AgileConfig").Exists())
    {
        config.AddAgileConfigCanReadTemplate(new ConfigClient(configRoot));
    }
});
builder.Host.ConfigureServices((hostingContext, services) =>
{
    // ��ʼ����ʱ����
    QuartzInit.InitJob();
});

builder.Services.AddControllers();

builder.Services.AddJwtConfig(builder.Configuration);
builder.Services.AddPolicies();

// ��ȡ������
//var swaggerPort = builder.Configuration.GetValue<int>("SwaggerPort");
//var enableSwagger = builder.Configuration.GetValue<bool>("EnableSwagger");

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGen(c =>
{
    //����Swagger
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "v1:�ӿ��ĵ�",
        Description = $"API�汾v1",
        Version = "v1",
        Contact = new OpenApiContact
        {
            Name = "FusionProgram",
            Email = string.Empty,
            Url = null,
        }
    });
    //����չʾע��
    {
        var path = Path.Combine(AppContext.BaseDirectory, "FusionProgram.xml");  // xml�ĵ�����·��
        c.IncludeXmlComments(path, true); // true : ��ʾ��������ע��
        c.OrderActionsBy(o => o.RelativePath); // ��action�����ƽ�����������ж�����Ϳ��Կ���Ч���ˡ�
    }
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Authorization header using the Bearer scheme. Example: Bearer 12345abcde",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Scheme = "bearer"
    });
    /*
    c.AddSecurityDefinition("Basic", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Please enter your username and password:",
        Type = SecuritySchemeType.Http,
        In = ParameterLocation.Header,
        Scheme = "basic"
    });*/
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Id = "Bearer",
                    Type = ReferenceType.SecurityScheme
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Id = "Basic",
                    Type = ReferenceType.SecurityScheme
                },
                Scheme = "oauth2",
                Name = "Basic",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
    //c.OperationFilter<AddParameterHeader>();
});

// ���CAP����ĳ�ʼ��
builder.Services.AddDbContext<CAP.ApplicationDbContext>(options => builder.Configuration.GetValue<string>("CAP:RedisConnectionString"));
builder.Services.AddCAP(builder.Configuration);
builder.Services.AddScoped<Publisher>();
builder.Services.AddTransient<MyMessageHandler>();

// ���redis
builder.Services.AddSingleton<IRedisServer, RedisServer>();

// ��ʼ��Dapper
DapperHelper.Initialize(builder.Configuration);

builder.Services.AddLogging(builder =>
{
    builder.AddFile();
});
// logger
builder.Services.AddLogging(x => {
    Log.Logger = new LoggerConfiguration()
        //.MinimumLevel.Debug()
        //.Enrich.FromLogContext()
        //.WriteTo.Console(new JsonFormatter())//����̨��־ 
        .WriteTo.File($"Logs/{DateTime.Now:yyyy-MM-dd}.log")//�ļ���־ 
        //.WriteTo.Exceptionless()//Exceptionless�ֲ�ʽ��־ 
        .CreateLogger();
    x.AddSerilog();
});

// ע�� IConfiguration
builder.Services.AddSingleton(builder.Configuration);

// ��������
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder => builder
                    .SetIsOriginAllowed((host) => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .WithOrigins("http://192.168.8.110:5600", "https://192.168.8.110:5101", "https://192.168.8.110:8090")); 
});

// ���SignalR
builder.Services.AddSignalR(opts =>
{
    opts.EnableDetailedErrors = true;
    opts.ClientTimeoutInterval = TimeSpan.FromMinutes(3000); // ���ÿͻ��˳�ʱʱ��
    opts.KeepAliveInterval = TimeSpan.FromSeconds(600000000); // ���ñ������ӵļ��
});

var app = builder.Build();

app.Use((context, next) =>
{
    // Log request details
    Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path}");

    return next();
});

app.UseHttpsRedirection();

app.UseRouting();

// ��ӿ�������
//��������Ҫ�����userouting��useendpoints�м�
//app.UseCors(options => options.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseCors();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    // �ֱ�ע���м����ui�м��
    app.UseSwagger();
    app.UseSwaggerUI();
//}

app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapHub<MyHub>("/myHub");
});

app.MapControllers();

app.Run();

