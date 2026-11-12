using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SalaoBeleza.Models;

namespace SalaoBeleza.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Barbeiro> Barbeiros { get; set; }
    public DbSet<Servico> Servicos { get; set; }
    public DbSet<Agendamento> Agendamentos { get; set; }
    public DbSet<AgendamentoServico> AgendamentoServicos { get; set; }
    public DbSet<BarbeiroFoto> BarbeiroFotos { get; set; }
    public DbSet<HorarioTrabalho> HorariosTrabalho { get; set; }
    public DbSet<Folga> Folgas { get; set; }
    public DbSet<Avaliacao> Avaliacoes { get; set; }
    public DbSet<Favorito> Favoritos { get; set; }
    public DbSet<ListaEspera> ListasEspera { get; set; }
    public DbSet<Notificacao> Notificacoes { get; set; }
    public DbSet<Cupom> Cupons { get; set; }
    public DbSet<RedefinicaoSenha> RedefinicoesSenha { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios");
            e.Property(u => u.Nome).HasMaxLength(150);
            e.Property(u => u.Email).HasMaxLength(150);
            e.Property(u => u.SenhaHash).HasMaxLength(255);
            e.Property(u => u.Tipo).HasMaxLength(20);
            e.Property(u => u.FotoUrl).HasMaxLength(300);
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Barbeiro>(e =>
        {
            e.ToTable("Barbeiros");
            e.Property(b => b.Especialidades).HasMaxLength(200);
            e.Property(b => b.Bio).HasMaxLength(1000);
            e.HasIndex(b => b.UsuarioId).IsUnique();

            e.HasOne(b => b.Usuario)
                .WithOne(u => u.Barbeiro)
                .HasForeignKey<Barbeiro>(b => b.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(b => b.Fotos)
                .WithOne(f => f.Barbeiro)
                .HasForeignKey(f => f.BarbeiroId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(b => b.Horarios)
                .WithOne(h => h.Barbeiro)
                .HasForeignKey(h => h.BarbeiroId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(b => b.Folgas)
                .WithOne(f => f.Barbeiro)
                .HasForeignKey(f => f.BarbeiroId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Servico>(e =>
        {
            e.ToTable("Servicos");
            e.Property(s => s.Nome).HasMaxLength(150);
        });

        modelBuilder.Entity<Agendamento>(e =>
        {
            e.ToTable("Agendamentos");
            e.Property(a => a.Status).HasMaxLength(20);
            e.Property(a => a.CupomCodigo).HasMaxLength(40);

            e.HasOne(a => a.Cliente)
                .WithMany(u => u.Agendamentos)
                .HasForeignKey(a => a.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Barbeiro)
                .WithMany(b => b.Agendamentos)
                .HasForeignKey(a => a.BarbeiroId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Servico)
                .WithMany(s => s.Agendamentos)
                .HasForeignKey(a => a.ServicoId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(a => a.Itens)
                .WithOne(i => i.Agendamento)
                .HasForeignKey(i => i.AgendamentoId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.Avaliacao)
                .WithOne(v => v.Agendamento)
                .HasForeignKey<Avaliacao>(v => v.AgendamentoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgendamentoServico>(e =>
        {
            e.ToTable("AgendamentoServicos");
            e.Property(i => i.Nome).HasMaxLength(150);
        });

        modelBuilder.Entity<BarbeiroFoto>(e =>
        {
            e.ToTable("BarbeiroFotos");
            e.Property(f => f.Url).HasMaxLength(300);
            e.Property(f => f.Legenda).HasMaxLength(150);
        });

        modelBuilder.Entity<HorarioTrabalho>(e =>
        {
            e.ToTable("HorariosTrabalho");
        });

        modelBuilder.Entity<Folga>(e =>
        {
            e.ToTable("Folgas");
            e.Property(f => f.Motivo).HasMaxLength(150);
        });

        modelBuilder.Entity<Avaliacao>(e =>
        {
            e.ToTable("Avaliacoes");
            e.Property(v => v.Comentario).HasMaxLength(500);
            e.HasIndex(v => v.AgendamentoId).IsUnique();

            e.HasOne(v => v.Cliente)
                .WithMany()
                .HasForeignKey(v => v.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(v => v.Barbeiro)
                .WithMany()
                .HasForeignKey(v => v.BarbeiroId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Favorito>(e =>
        {
            e.ToTable("Favoritos");
            e.HasIndex(f => new { f.ClienteId, f.BarbeiroId }).IsUnique();
        });

        modelBuilder.Entity<ListaEspera>(e =>
        {
            e.ToTable("ListaEspera");
            e.Property(l => l.ServicoIds).HasMaxLength(100);
            e.Property(l => l.Status).HasMaxLength(20);

            e.HasOne(l => l.Cliente)
                .WithMany()
                .HasForeignKey(l => l.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(l => l.Barbeiro)
                .WithMany()
                .HasForeignKey(l => l.BarbeiroId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notificacao>(e =>
        {
            e.ToTable("Notificacoes");
            e.Property(n => n.Titulo).HasMaxLength(150);
            e.Property(n => n.Mensagem).HasMaxLength(500);
        });

        modelBuilder.Entity<Cupom>(e =>
        {
            e.ToTable("Cupons");
            e.Property(c => c.Codigo).HasMaxLength(40);
            e.Property(c => c.Tipo).HasMaxLength(20);
            e.HasIndex(c => c.Codigo).IsUnique();
        });

        modelBuilder.Entity<RedefinicaoSenha>(e =>
        {
            e.ToTable("RedefinicoesSenha");
            e.Property(r => r.TokenHash).HasMaxLength(100);
        });

        // Convencao do banco: colunas em snake_case (nome_completo), datas sem fuso, horas como "time".
        foreach (var entidade in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var propriedade in entidade.GetProperties())
            {
                propriedade.SetColumnName(ParaSnakeCase(propriedade.Name));

                if (propriedade.ClrType == typeof(DateTime) || propriedade.ClrType == typeof(DateTime?))
                    propriedade.SetColumnType("timestamp without time zone");

                if (propriedade.ClrType == typeof(TimeSpan) || propriedade.ClrType == typeof(TimeSpan?))
                    propriedade.SetColumnType("time without time zone");

                if (propriedade.ClrType == typeof(decimal) || propriedade.ClrType == typeof(decimal?))
                {
                    propriedade.SetPrecision(10);
                    propriedade.SetScale(2);
                }
            }
        }
    }

    private static string ParaSnakeCase(string nome)
    {
        return Regex.Replace(nome, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
    }
}
