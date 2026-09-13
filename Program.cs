using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PosWebApi.Data;
using PosWebApi.Services;

// QuestPDF Community license: free for organizations with <$1M USD annual revenue.
// Revisit this if that ever changes - see https://www.questpdf.com/license/.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// 1. Connection string configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=127.0.0.1;Port=3306;Database=PosDb;Uid=root;Pwd=CHANGE_ME;";

// 2. Register DbContext with Pomelo MySQL. Pooled (not plain AddDbContext) since AppDbContext
// is stateless beyond what DbContextOptions supplies (no per-instance fields set outside the
// constructor) - safe to reuse pooled instances across requests, avoiding a fresh DbContext
// allocation on every one of this API's many short-lived, request-scoped database calls.
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)), poolSize: 128);

// 3. Register Repositories
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddHttpContextAccessor();

// Redis-backed distributed cache for catalog reads (CatalogManager.GetAllCached/GetByIdCached).
// Falls back to "localhost:6379" if Redis:ConnectionString isn't configured.
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379");

// 4. Register Application Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<CatalogManager>();
builder.Services.AddScoped<IPosEngine, PosEngine>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<ShiftService>();
builder.Services.AddScoped<ReceiptService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<DiscountCodeService>();
builder.Services.AddScoped<RegisterService>();
builder.Services.AddScoped<RefundService>();
builder.Services.AddScoped<AuditService>();

// 5. Configure JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtSecret))
    throw new InvalidOperationException("JWT:Secret is not configured in appsettings.json");

if (string.IsNullOrWhiteSpace(jwtIssuer))
    throw new InvalidOperationException("JWT:Issuer is not configured in appsettings.json");

if (string.IsNullOrWhiteSpace(jwtAudience))
    throw new InvalidOperationException("JWT:Audience is not configured in appsettings.json");

var keyBytes = System.Text.Encoding.UTF8.GetBytes(jwtSecret);
if (keyBytes.Length < 32)
    throw new InvalidOperationException("JWT:Secret must be at least 256 bits (32 bytes)");

var key = new SymmetricSecurityKey(keyBytes);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Improve error messages in development
        if (builder.Environment.IsDevelopment())
        {
            options.Events = new JwtBearerEvents
            {
                // OnAuthenticationFailed fires mid-pipeline, before the framework's own
                // challenge response is produced. Writing to the response here directly (as
                // this used to) leaves the response "started", so the later challenge write
                // (OnChallenge/HandleChallengeAsync) throws "StatusCode cannot be set because
                // the response has already started" - surfacing a 500 instead of a clean 401
                // for a malformed/expired bearer token. Just stash the failure reason instead.
                OnAuthenticationFailed = context =>
                {
                    context.HttpContext.Items["JwtAuthError"] = context.Exception.Message;
                    return Task.CompletedTask;
                },
                // OnChallenge is the correct place to write a custom 401 body: HandleResponse()
                // suppresses the framework's default challenge response so ours is the only write.
                OnChallenge = context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    var details = context.HttpContext.Items.TryGetValue("JwtAuthError", out var msg)
                        ? msg as string
                        : "Authentication required";
                    var response = new { error = "Unauthorized", details };
                    return context.Response.WriteAsJsonAsync(response);
                },
                OnForbidden = context =>
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";
                    var response = new { error = "Forbidden", details = "You do not have permission to access this resource" };
                    return context.Response.WriteAsJsonAsync(response);
                }
            };
        }
    });

// 6. Add authorization policies
builder.Services.AddAuthorization();

// 6b. CORS for the local React dev server (Vite default ports). Credentials aren't used
// (the JWT goes in an Authorization header, not a cookie), so no AllowCredentials() needed.
const string FrontendCorsPolicy = "FrontendDev";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// 7. Add Controllers & NewtonsoftJson
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);

// 8. Configure Swagger/OpenAPI with JWT Security Definition
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Add JWT Bearer security scheme
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = @"JWT Authorization header using the Bearer scheme. 
                      Enter 'Bearer' [space] and then your token in the text input below.
                      Example: 'Bearer eyJhbGciOiJIUzI1NiIs...'",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // Add security requirement for protected endpoints
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });

    // Add XML comments support for Swagger documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (System.IO.File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// 9. Configure HTTP Request Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "POS System API v1");
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
    });
}

app.UseHttpsRedirection();

// UseCors must run before Authentication/Authorization so preflight OPTIONS requests
// (which never carry a bearer token) aren't rejected before CORS headers are attached.
app.UseCors(FrontendCorsPolicy);

// 10. Middleware ordering is CRITICAL: Authentication must come BEFORE Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
