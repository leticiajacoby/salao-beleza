using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SalaoBeleza.Common;
using SalaoBeleza.Data;
using SalaoBeleza.Services;

var builder = WebApplication.CreateBuilder(args);

// Banco: no Render basta definir DATABASE_URL (postgres://usuario:senha@host/banco).
// Localmente vale a string "ConnectionStrings:Postgres" do appsettings.json.
static string MontarConexao(IConfiguration config)
{
    var url = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (string.IsNullOrWhiteSpace(url))
        return config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:Postgres ou DATABASE_URL.");
    if (!url.StartsWith("postgres://") && !url.StartsWith("postgresql://"))
        return url; // ja no formato do Npgsql

    var uri = new Uri(url);
    var credenciais = uri.UserInfo.Split(':', 2);
    var porta = uri.Port > 0 ? uri.Port : 5432;
    return $"Host={uri.Host};Port={porta};Database={uri.AbsolutePath.TrimStart('/')};" +
           $"Username={Uri.UnescapeDataString(credenciais[0])};Password={Uri.UnescapeDataString(credenciais.ElementAtOrDefault(1) ?? "")};" +
           "SSL Mode=Prefer;Trust Server Certificate=true";
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(MontarConexao(builder.Configuration)));

// Servicos (regras de negocio)
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<ArquivoService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<ServicoService>();
builder.Services.AddScoped<BarbeiroService>();
builder.Services.AddScoped<ClienteService>();
builder.Services.AddScoped<AgendamentoService>();
builder.Services.AddScoped<DisponibilidadeService>();
builder.Services.AddScoped<CupomService>();
builder.Services.AddScoped<FidelidadeService>();
builder.Services.AddScoped<NotificacaoService>();
builder.Services.AddScoped<AvaliacaoService>();
builder.Services.AddScoped<FavoritoService>();
builder.Services.AddScoped<ListaEsperaService>();
builder.Services.AddScoped<RelatorioService>();

builder.Services
    .AddControllers(opcoes =>
    {
        // As mensagens de validacao em portugues vem dos Services.
        opcoes.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .ConfigureApiBehaviorOptions(opcoes =>
    {
        // Corpo da requisicao invalido -> sempre JSON { "mensagem": "..." }
        opcoes.InvalidModelStateResponseFactory = contexto =>
            new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { mensagem = "Dados invalidos. Confira os campos e tente novamente." });
    });

// Autenticacao por token JWT
var chaveJwt = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(chaveJwt) || chaveJwt.Length < 32)
    throw new InvalidOperationException("Configure Jwt:Key no appsettings.json com pelo menos 32 caracteres.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opcoes =>
    {
        opcoes.MapInboundClaims = false;
        opcoes.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chaveJwt)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = "role",
            NameClaimType = "sub"
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<TratadorDeErros>();

// Swagger so em desenvolvimento.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Cria/atualiza as tabelas e o administrador inicial.
await BancoInicializador.Inicializar(app.Services);

app.Run();
