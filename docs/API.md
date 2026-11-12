# API do Spa Delas

Base: mesma origem do site (`/api/...`). JSON em camelCase. Datas/horas **sem fuso** (horário local do salão):
`"2026-10-20T14:30:00"`. Autenticação: `Authorization: Bearer <token>` (use `api()` do `comum.js`, que já faz isso).
Erros: HTTP 4xx/5xx com corpo `{ "mensagem": "texto em português" }`.

Perfis (`tipo`): `Cliente`, `Barbeiro`, `Administrador`. Status de agendamento: `Agendado`, `Concluido`, `Faltou`, `Cancelado`.

## Usuários / conta
| Método e rota | Quem | Corpo → Resposta |
|---|---|---|
| POST `/api/usuarios/registrar` | público | `{nome,email,senha}` → `Auth` |
| POST `/api/usuarios/login` | público | `{email,senha}` → `Auth` (401 se inválido; 429 após 5 falhas) |
| POST `/api/usuarios/esqueci-senha` | público | `{email}` → `{mensagem, linkDev}` (`linkDev` só em desenvolvimento sem SMTP) |
| POST `/api/usuarios/redefinir-senha` | público | `{token,novaSenha}` → `{mensagem}` |
| GET `/api/usuarios/me` | logado | → `Usuario` |
| PUT `/api/usuarios/me` | logado | `{nome,email}` → `Usuario` |
| PUT `/api/usuarios/me/senha` | logado | `{senhaAtual,novaSenha}` → `{mensagem}` |
| POST `/api/usuarios/me/foto` | logado | multipart, campo `arquivo` → `Usuario` |
| DELETE `/api/usuarios/me/foto` | logado | → `Usuario` |

`Auth` = `{ token, usuario }`. `Usuario` = `{ id, nome, email, tipo, barbeiroId (int|null), fotoUrl (string|null) }`.
Depois de alterar dados/foto, chame `Sessao.atualizarUsuario(usuarioRetornado)`.

## Serviços
| Rota | Quem | Notas |
|---|---|---|
| GET `/api/servicos` | público | só ativos; admin pode `?todos=true`. → `Servico[]` |
| GET `/api/servicos/{id}` | público | |
| POST `/api/servicos` | admin | `{nome,preco,duracaoMinutos}` → `Servico` |
| PUT `/api/servicos/{id}` | admin | idem |
| PATCH `/api/servicos/{id}/ativo` | admin | `{ativo:bool}` → `Servico` |
| DELETE `/api/servicos/{id}` | admin | 409 se já usado em agendamento (peça para desativar) |

`Servico` = `{ id, nome, preco, duracaoMinutos, ativo }`.

## Barbeiros
| Rota | Quem | Notas |
|---|---|---|
| GET `/api/barbeiros` | público | só ativos; admin `?todos=true`. → `BarbeiroLista[]` |
| GET `/api/barbeiros/{id}/perfil` | público | → `BarbeiroPerfil` |
| POST `/api/barbeiros` | admin | `{nome,email,senha,percentualBarbeiro}` → `BarbeiroLista` |
| PUT `/api/barbeiros/{id}` | admin | `{nome,email,novaSenha (opcional),percentualBarbeiro}` → `BarbeiroLista` |
| PATCH `/api/barbeiros/{id}/ativo` | admin | `{ativo}` → `{ativo}` |
| DELETE `/api/barbeiros/{id}` | admin | 409 se tem histórico |
| PUT `/api/barbeiros/{id}/perfil` | admin ou o próprio barbeiro | `{especialidades,bio}` → `BarbeiroPerfil` |
| POST `/api/barbeiros/{id}/portfolio` | admin/próprio | multipart `arquivo` + `legenda` → `FotoPortfolio` (máx. 12) |
| DELETE `/api/barbeiros/{id}/portfolio/{fotoId}` | admin/próprio | 204 |
| GET `/api/barbeiros/{id}/horarios` | admin/próprio | → `Horario[]` (7 dias, `diaSemana` 0=domingo … 6=sábado) |
| PUT `/api/barbeiros/{id}/horarios` | admin/próprio | corpo: `Horario[]` (envie os 7 dias) → `Horario[]` |
| GET `/api/barbeiros/{id}/folgas` | admin/próprio | folgas de hoje em diante → `Folga[]` |
| POST `/api/barbeiros/{id}/folgas` | admin/próprio | `{dataInicio,dataFim,motivo}` (datas "YYYY-MM-DD", dias inteiros, inclusivas) → `{folga, agendamentosAfetados}` |
| DELETE `/api/barbeiros/{id}/folgas/{folgaId}` | admin/próprio | 204 |
| GET `/api/barbeiros/{id}/ganhos?mes=2026-10` | admin/próprio | → `Ganhos` |

`BarbeiroLista` = `{ id, usuarioId, nome, email (só admin), fotoUrl, especialidades, ativo, media (0-5, 0 = sem avaliações), totalAvaliacoes, percentualBarbeiro (só admin/próprio, senão null) }`.
`BarbeiroPerfil` = `{ id, nome, fotoUrl, especialidades, bio, ativo, media, totalAvaliacoes, fotos: FotoPortfolio[], avaliacoes: [{id,nomeCliente (só 1º nome),nota,comentario,data}] }`.
`FotoPortfolio` = `{ id, url, legenda }`.
`Horario` = `{ diaSemana, trabalha, inicio:"09:00", fim:"18:00", almocoInicio:"12:00"|null, almocoFim:"13:00"|null }`.
`Folga` = `{ id, dataInicio, dataFim, motivo }` (datas ISO; ambas inclusivas).
`Ganhos` = `{ mes:"2026-10", concluidos, faltas, cancelados, agendados, totalRecebido, totalPrevisto }` (valores = comissão do barbeiro).

## Agendamentos
| Rota | Quem | Notas |
|---|---|---|
| GET `/api/agendamentos?de=&ate=&barbeiroId=&status=` | logado | cada perfil só vê o que é seu; admin vê todos. `de` inclusivo, `ate` exclusivo (`2026-10-12T00:00:00`). → `Agendamento[]` ordenado por data |
| GET `/api/agendamentos/{id}` | dono/admin | |
| GET `/api/agendamentos/horarios-livres?barbeiroId=&data=2026-10-20&servicoIds=1,2` | logado | alternativa a `servicoIds`: `duracao=60`. Ao reagendar: `&ignorarAgendamentoId=5&duracao=60`. → `["09:00","09:30",...]` (vazio = sem vagas) |
| POST `/api/agendamentos/simular` | cliente/admin | `{servicoIds, cupomCodigo, usarFidelidade}` → `Simulacao` |
| POST `/api/agendamentos` | cliente/admin | `{dataHora, barbeiroId, servicoIds:[1,2], cupomCodigo|null, usarFidelidade:bool}` (admin: + `clienteId`) → `Agendamento` |
| PUT `/api/agendamentos/{id}/reagendar` | cliente dono/admin | `{dataHora, barbeiroId (opcional)}` → `Agendamento` |
| PUT `/api/agendamentos/{id}/cancelar` | cliente dono/barbeiro/admin | → `Agendamento` |
| PUT `/api/agendamentos/{id}/status` | barbeiro do agendamento/admin | `{status:"Concluido"|"Faltou"}` → `Agendamento` (Faltou só após o horário) |
| DELETE `/api/agendamentos/{id}` | admin | apaga de vez, 204 |

`Agendamento` = `{ id, dataHora, fim, duracaoMinutos, status, clienteId, nomeCliente, fotoCliente, barbeiroId, nomeBarbeiro, fotoBarbeiro, servicos:[{servicoId,nome,preco,duracaoMinutos}], nomeServico ("Corte + Barba"), valorServico (bruto), valorDesconto, valorFinal (o que o cliente paga), percentualBarbeiro, valorBarbeiro (comissão), valorAdministrador, cupomCodigo, usouFidelidade, avaliacao: {nota,comentario}|null }`.
`Simulacao` = `{ valorServico, valorDesconto, valorFinal, duracaoMinutos, cupomCodigo, usouFidelidade, aviso (texto quando o cupom/fidelidade não pôde ser aplicado) }`.

Regras: horários oferecidos de 30 em 30 min dentro do expediente do barbeiro, sem almoço, sem folga e sem conflito. Cupom e fidelidade não se combinam.
Faturamento = só `Concluido`, usando `valorFinal`.

## Avaliações, favoritos, lista de espera, fidelidade, cupons, notificações
| Rota | Quem | Notas |
|---|---|---|
| POST `/api/avaliacoes` | cliente | `{agendamentoId,nota (1-5),comentario}` → `{nota,comentario}` (só agendamento `Concluido`, 1 por agendamento) |
| GET `/api/favoritos` | cliente | → `[barbeiroId,...]` |
| POST `/api/favoritos/{barbeiroId}` | cliente | → lista atualizada de ids |
| DELETE `/api/favoritos/{barbeiroId}` | cliente | → lista atualizada de ids |
| GET `/api/lista-espera` | cliente (os seus) / admin (todos) | → `ListaEspera[]` (de hoje em diante) |
| POST `/api/lista-espera` | cliente | `{barbeiroId,data:"2026-10-20",servicoIds:[1]}` → `ListaEspera` |
| DELETE `/api/lista-espera/{id}` | dono/admin | 204 |
| GET `/api/fidelidade` | cliente | → `Fidelidade` |
| GET `/api/cupons/validar?codigo=X&valor=80` | logado | → `{valido,mensagem,codigo,tipo,valor,desconto}` |
| GET/POST `/api/cupons`, PUT `/api/cupons/{id}`, PATCH `/api/cupons/{id}/ativo` `{ativo}`, DELETE `/api/cupons/{id}` | admin | `CupomSalvar` = `{codigo,tipo:"Percentual"|"Valor",valor,validade:"YYYY-MM-DD"|null,usosMaximos:int|null}` → `Cupom` |
| GET `/api/notificacoes` | logado | → `{naoLidas, itens:[{id,titulo,mensagem,lida,criadoEm}]}` (o cabeçalho já cuida disso) |

`ListaEspera` = `{ id, barbeiroId, nomeBarbeiro, clienteId, nomeCliente, data, servicos (texto), servicoIds, status:"Aguardando"|"Notificado" }`.
`Fidelidade` = `{ meta:10, progresso (0-9), pagosConcluidos, gratisGanhos, gratisUsados, gratisDisponiveis }`.
`Cupom` = `{ id, codigo, tipo, valor, validade, usosMaximos, usosAtuais, ativo }`.
Fidelidade: a cada 10 serviços **pagos** concluídos o cliente ganha 1 grátis (`usarFidelidade:true` no POST de agendamento).
Comissão: calculada sobre `valorServico` (bruto); o salão absorve descontos e serviços grátis (`valorAdministrador` pode ser ≤ 0).

## Admin
| Rota | Notas |
|---|---|
| GET `/api/clientes` | → `[{id,nome,email,fotoUrl,totalAgendamentos}]` |
| DELETE `/api/clientes/{id}` | 409 se tem histórico |
| GET `/api/dashboard?meses=6` | → `{ faturamentoMes, faturamentoMesAnterior, atendimentosMes, ticketMedio, agendamentosHoje, agendamentosFuturos, totalClientes, faltas, faturamentoPorMes:[{mes:"2026-10",faturamento,atendimentos}], servicosMaisPedidos:[{nome,quantidade,faturamento}], rankingBarbeiros:[{barbeiroId,nome,fotoUrl,atendimentos,faturamento,comissao,media}], horariosPico:[{hora,quantidade}] }` |
