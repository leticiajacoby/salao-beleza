# Spa Delas (SalaoBeleza)

API em **.NET 10** + **EF Core** + **PostgreSQL 16 (Docker)** com frontend em HTML/JS puro (`wwwroot`).

## Como rodar

```bash
docker compose up -d      # sobe o PostgreSQL (porta 5433)
dotnet restore
dotnet run                # http://localhost:5000
```

As tabelas são criadas/atualizadas sozinhas na primeira execução (SQL idempotente em `Data/BancoInicializador.cs`; não usa Migrations).
Se você já tinha um banco da versão antiga, o sistema avisa; nesse caso apague o volume: `docker compose down -v` e suba de novo.

Abra `http://localhost:5000`.

## Contas

- Administrador criado no primeiro início: `admin@salao.com` / `admin123` (configurável em `appsettings.json` → `Admin`). **Troque a senha e a `Jwt:Key` antes de publicar.**
- Clientes se cadastram na tela inicial. As profissionais são cadastradas pelo administrador.

## O que tem

- Login com JWT, senha com PBKDF2, bloqueio após 5 falhas, "esqueci a senha" (token de 30 min).
- Cliente: início com próximos agendamentos e serviços; agendar combos de serviços; cupom e fidelidade (a cada 10 serviços, 1 grátis); reagendar, cancelar, repetir; avaliar 1–5 ★; favoritar profissionais; lista de espera; conta com foto de perfil.
- Profissional: agenda em calendário (dia/semana), concluir/faltou/cancelar, ganhos do mês, horários de trabalho, almoço e folgas, perfil público com especialidades e portfólio, avaliações.
- Administrador: dashboard com gráficos (faturamento por mês, serviços mais pedidos, ranking, horários de pico), agenda geral, agendamentos, CRUD com editar/ativar/desativar de serviços, profissionais e cupons, clientes, financeiro.
- Perfil público da profissional: `perfil.html?id=1`.

## Configuração (`appsettings.json`)

- Em produção (Render): `DATABASE_URL`, `Jwt__Key`, `Admin__Email`, `Admin__Senha`, `App__UrlBase` (veja `render.yaml`).
- `ConnectionStrings:Postgres`, `Jwt:Key` (mín. 32 caracteres), `Admin:*`, `App:UrlBase`.
- `Smtp:*` para envio real do e-mail de redefinição. Sem SMTP, em desenvolvimento o link aparece na própria tela; fora dele, vai para o log.
- Swagger só em Development: `/swagger`.

## Documentação

`docs/API.md` descreve todas as rotas e DTOs. `tools/mock-server.js` é um servidor de teste do frontend (`node tools/mock-server.js 5100`), sem banco.


> Nota: por compatibilidade com o banco e a API, os nomes internos (`/api/barbeiros`, tabela `Barbeiros`, perfil `Barbeiro`) continuam iguais; na interface aparecem como **Profissional**.
