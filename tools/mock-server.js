// Servidor de TESTE do frontend (nao e o backend real). Uso: node mock-server.js 5101
// Serve ../wwwroot e simula a API descrita em docs/API.md com dados em memoria.
const http = require("http");
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");

const PORTA = Number(process.argv[2] || 5100);
const RAIZ = path.join(__dirname, "..", "wwwroot");

/* ---------- utilidades de data (horario local, sem fuso) ---------- */
const dois = (n) => String(n).padStart(2, "0");
const isoData = (d) => `${d.getFullYear()}-${dois(d.getMonth() + 1)}-${dois(d.getDate())}`;
const iso = (d) => `${isoData(d)}T${dois(d.getHours())}:${dois(d.getMinutes())}:00`;
const ler = (s) => { const [d, h] = String(s).split("T"); const [a, m, di] = d.split("-").map(Number); const [hh, mm] = (h || "00:00").split(":").map(Number); return new Date(a, m - 1, di, hh || 0, mm || 0, 0); };
const dia = (d) => new Date(d.getFullYear(), d.getMonth(), d.getDate());
const somar = (d, n) => { const r = new Date(d); r.setDate(r.getDate() + n); return r; };
const agora = () => new Date();
const r2 = (n) => Math.round(n * 100) / 100;

/* ---------- dados ---------- */
let seq = 100;
const prox = () => ++seq;
const db = {
  usuarios: [
    { id: 1, nome: "Administrador", email: "admin@salao.com", senha: "admin123", tipo: "Administrador", fotoUrl: null },
    { id: 2, nome: "Maria Souza", email: "cliente@teste.com", senha: "123456", tipo: "Cliente", fotoUrl: null },
    { id: 3, nome: "Camila Lima", email: "profissional@teste.com", senha: "123456", tipo: "Barbeiro", fotoUrl: null },
    { id: 4, nome: "Ana Paula Reis", email: "ana@teste.com", senha: "123456", tipo: "Barbeiro", fotoUrl: null },
    { id: 5, nome: "Juliana Pedro Alves", email: "juliana@teste.com", senha: "123456", tipo: "Cliente", fotoUrl: null },
  ],
  barbeiros: [
    { id: 1, usuarioId: 3, percentual: 60, ativo: true, especialidades: "Mechas, escova, corte feminino", bio: "10 anos de experiência com cortes, mechas e finalização. Atendimento sem pressa.", fotos: [], },
    { id: 2, usuarioId: 4, percentual: 70, ativo: true, especialidades: "Coloração, hidratação", bio: "", fotos: [] },
  ],
  servicos: [
    { id: 1, nome: "Corte feminino", preco: 70, duracaoMinutos: 30, ativo: true },
    { id: 2, nome: "Escova", preco: 50, duracaoMinutos: 30, ativo: true },
    { id: 3, nome: "Manicure", preco: 35, duracaoMinutos: 30, ativo: true },
    { id: 4, nome: "Hidratação", preco: 60, duracaoMinutos: 60, ativo: true },
    { id: 5, nome: "Coloração", preco: 120, duracaoMinutos: 90, ativo: true },
    { id: 6, nome: "Alisamento (descontinuado)", preco: 10, duracaoMinutos: 30, ativo: false },
  ],
  horarios: {}, // barbeiroId -> Horario[]
  folgas: [],
  agendamentos: [],
  avaliacoes: [],
  favoritos: [{ clienteId: 2, barbeiroId: 1 }],
  espera: [],
  notificacoes: [],
  cupons: [
    { id: 1, codigo: "BEMVINDO10", tipo: "Percentual", valor: 10, validade: null, usosMaximos: null, usosAtuais: 3, ativo: true },
    { id: 2, codigo: "CINCO", tipo: "Valor", valor: 5, validade: isoData(somar(agora(), 30)), usosMaximos: 100, usosAtuais: 0, ativo: true },
    { id: 3, codigo: "VELHO", tipo: "Percentual", valor: 50, validade: isoData(somar(agora(), -5)), usosMaximos: null, usosAtuais: 0, ativo: true },
  ],
  resets: [],
};

function horariosPadrao() {
  const l = [];
  for (let d = 0; d <= 6; d++) l.push({ diaSemana: d, trabalha: d !== 0, inicio: "09:00", fim: "18:00", almocoInicio: "12:00", almocoFim: "13:00" });
  return l;
}
const horariosDe = (bid) => db.horarios[bid] || horariosPadrao();
const usuario = (id) => db.usuarios.find((u) => u.id === id);
const barbeiro = (id) => db.barbeiros.find((b) => b.id === id);
const servico = (id) => db.servicos.find((s) => s.id === id);

function criarAg({ clienteId, barbeiroId, servicoIds, data, status = "Agendado", cupomCodigo = null, fidel = false, desconto = 0 }) {
  const itens = servicoIds.map((id) => servico(id));
  const total = itens.reduce((s, i) => s + i.preco, 0);
  const dur = itens.reduce((s, i) => s + i.duracaoMinutos, 0);
  const b = barbeiro(barbeiroId);
  const com = r2((total * b.percentual) / 100);
  const final = total - desconto;
  const a = {
    id: prox(), dataHora: iso(data), duracaoMinutos: dur, status, clienteId, barbeiroId,
    servicos: itens.map((i) => ({ servicoId: i.id, nome: i.nome, preco: i.preco, duracaoMinutos: i.duracaoMinutos })),
    valorServico: total, valorDesconto: desconto, valorFinal: final, percentualBarbeiro: b.percentual,
    valorBarbeiro: com, valorAdministrador: r2(final - com), cupomCodigo, usouFidelidade: fidel, avaliacao: null,
  };
  db.agendamentos.push(a);
  return a;
}

// Historico de exemplo
(function seed() {
  const hoje = dia(agora());
  const combos = [[1], [1, 2], [2], [4], [1], [1, 2], [3], [1], [5], [1, 2], [1], [4], [1, 2]];
  combos.forEach((c, i) => {
    const d = somar(hoje, -(i * 6 + 3));
    d.setHours(9 + (i % 7), i % 2 ? 30 : 0, 0, 0);
    if (d.getDay() === 0) d.setDate(d.getDate() + 1);
    const a = criarAg({ clienteId: i % 4 === 3 ? 5 : 2, barbeiroId: i % 3 === 2 ? 2 : 1, servicoIds: c, data: d, status: "Concluido" });
    if (i % 3 === 0) { a.avaliacao = { nota: 5 - (i % 2), comentario: i % 2 ? "Muito bom, recomendo!" : "Atendimento excelente." }; db.avaliacoes.push({ id: prox(), agendamentoId: a.id, barbeiroId: a.barbeiroId, clienteId: a.clienteId, nota: a.avaliacao.nota, comentario: a.avaliacao.comentario, criadoEm: iso(d) }); }
  });
  // um passado agendado (para marcar concluido/faltou) e futuros
  const ontem = somar(hoje, -1); ontem.setHours(15, 0, 0, 0);
  criarAg({ clienteId: 2, barbeiroId: 1, servicoIds: [1], data: ontem });
  let amanha = somar(hoje, 1); if (amanha.getDay() === 0) amanha = somar(amanha, 1); amanha.setHours(10, 0, 0, 0);
  criarAg({ clienteId: 2, barbeiroId: 1, servicoIds: [1, 2], data: amanha });
  const h = agora(); const hojeTarde = new Date(hoje); hojeTarde.setHours(Math.min(h.getHours() + 2, 17), 0, 0, 0);
  if (hojeTarde > h && hoje.getDay() !== 0) criarAg({ clienteId: 5, barbeiroId: 1, servicoIds: [1], data: hojeTarde });
  let depois = somar(hoje, 3); if (depois.getDay() === 0) depois = somar(depois, 1); depois.setHours(14, 0, 0, 0);
  criarAg({ clienteId: 5, barbeiroId: 2, servicoIds: [5], data: depois });
  db.notificacoes.push({ id: prox(), usuarioId: 2, titulo: "Bem-vinda!", mensagem: "Seu cadastro foi concluído.", lida: false, criadoEm: iso(agora()) });
  db.notificacoes.push({ id: prox(), usuarioId: 3, titulo: "Novo agendamento", mensagem: "Maria Souza agendou Corte feminino + Escova.", lida: false, criadoEm: iso(agora()) });
})();

/* ---------- regras ---------- */
class Erro extends Error { constructor(msg, status = 400) { super(msg); this.status = status; } }

function dtoUsuario(u) {
  const b = db.barbeiros.find((x) => x.usuarioId === u.id);
  return { id: u.id, nome: u.nome, email: u.email, tipo: u.tipo, barbeiroId: b ? b.id : null, fotoUrl: u.fotoUrl };
}
const token = (u) => "mock." + Buffer.from(JSON.stringify({ id: u.id })).toString("base64");
const auth = (u) => ({ token: token(u), usuario: dtoUsuario(u) });

function media(bid) {
  const l = db.avaliacoes.filter((a) => a.barbeiroId === bid);
  return { media: l.length ? Math.round((l.reduce((s, a) => s + a.nota, 0) / l.length) * 10) / 10 : 0, total: l.length };
}
function dtoBarbeiro(b, user) {
  const u = usuario(b.usuarioId); const m = media(b.id);
  const admin = user && user.tipo === "Administrador"; const proprio = user && b.usuarioId === user.id;
  return { id: b.id, usuarioId: b.usuarioId, nome: u.nome, email: admin ? u.email : null, fotoUrl: u.fotoUrl, especialidades: b.especialidades, ativo: b.ativo, media: m.media, totalAvaliacoes: m.total, percentualBarbeiro: admin || proprio ? b.percentual : null };
}
function dtoAg(a) {
  const c = usuario(a.clienteId); const b = barbeiro(a.barbeiroId); const ub = usuario(b.usuarioId);
  const ini = ler(a.dataHora);
  return { ...a, fim: iso(new Date(ini.getTime() + a.duracaoMinutos * 60000)), nomeCliente: c.nome, fotoCliente: c.fotoUrl, nomeBarbeiro: ub.nome, fotoBarbeiro: ub.fotoUrl, nomeServico: a.servicos.map((s) => s.nome).join(" + ") };
}
function notificar(usuarioId, titulo, mensagem) { db.notificacoes.push({ id: prox(), usuarioId, titulo, mensagem, lida: false, criadoEm: iso(agora()) }); }
const quando = (d) => `${dois(d.getDate())}/${dois(d.getMonth() + 1)} às ${dois(d.getHours())}:${dois(d.getMinutes())}`;

function slots(bid, dataIso, dur, ignorar) {
  const d0 = dia(ler(dataIso)); const now = agora();
  if (d0 < dia(now) || dur <= 0) return [];
  const h = horariosDe(bid).find((x) => x.diaSemana === d0.getDay());
  if (!h || !h.trabalha) return [];
  const hm = (s) => { const [a, b] = s.split(":").map(Number); return a * 60 + b; };
  const fimD = somar(d0, 1);
  const folgas = db.folgas.filter((f) => f.barbeiroId === bid);
  const ocupados = db.agendamentos.filter((a) => a.barbeiroId === bid && a.id !== ignorar && (a.status === "Agendado" || a.status === "Concluido") && ler(a.dataHora) >= d0 && ler(a.dataHora) < fimD);
  const out = [];
  for (let m = hm(h.inicio); m + dur <= hm(h.fim); m += 30) {
    const s = new Date(d0.getTime() + m * 60000); const e = new Date(s.getTime() + dur * 60000);
    if (s <= now) continue;
    if (h.almocoInicio && h.almocoFim) { const ai = hm(h.almocoInicio), af = hm(h.almocoFim); if (m < af && m + dur > ai) continue; }
    if (folgas.some((f) => s < somar(ler(f.dataFim), 1) && e > ler(f.dataInicio))) continue;
    if (ocupados.some((o) => s < new Date(ler(o.dataHora).getTime() + o.duracaoMinutos * 60000) && e > ler(o.dataHora))) continue;
    out.push(`${dois(s.getHours())}:${dois(s.getMinutes())}`);
  }
  return out;
}
function fidelidade(clienteId) {
  const pagos = db.agendamentos.filter((a) => a.clienteId === clienteId && a.status === "Concluido" && !a.usouFidelidade).length;
  const usados = db.agendamentos.filter((a) => a.clienteId === clienteId && a.usouFidelidade && a.status !== "Cancelado").length;
  const ganhos = Math.floor(pagos / 10);
  return { meta: 10, progresso: pagos % 10, pagosConcluidos: pagos, gratisGanhos: ganhos, gratisUsados: usados, gratisDisponiveis: Math.max(0, ganhos - usados) };
}
function aplicarCupom(codigo, valor) {
  const c = db.cupons.find((x) => x.codigo === String(codigo || "").trim().toUpperCase());
  if (!c || !c.ativo) throw new Erro("Cupom invalido.");
  if (c.validade && ler(c.validade) < dia(agora())) throw new Erro("Este cupom expirou.");
  if (c.usosMaximos && c.usosAtuais >= c.usosMaximos) throw new Erro("Este cupom atingiu o limite de usos.");
  let d = c.tipo === "Percentual" ? r2((valor * c.valor) / 100) : c.valor;
  return { cupom: c, desconto: Math.min(d, valor) };
}
function listaServicos(ids) {
  const d = [...new Set(ids || [])];
  if (!d.length) throw new Erro("Escolha pelo menos um servico.");
  const l = d.map((id) => servico(id));
  if (l.some((s) => !s || !s.ativo)) throw new Erro("Um dos servicos escolhidos nao esta mais disponivel.");
  return l;
}
function liberarEspera(bid, dt) {
  const dd = isoData(dt);
  db.espera.filter((e) => e.barbeiroId === bid && e.data === dd && e.status === "Aguardando").forEach((e) => {
    notificar(e.clienteId, "Abriu um horário!", `Liberou uma vaga com ${usuario(barbeiro(bid).usuarioId).nome} em ${quando(dt)}. Corra para agendar!`);
    e.status = "Notificado";
  });
}
const exigir = (u, ...tipos) => { if (!u) throw new Erro("Sua sessão expirou. Entre novamente.", 401); if (tipos.length && !tipos.includes(u.tipo)) throw new Erro("Você não tem permissão para isso.", 403); };
const podeBarbeiro = (u, id) => { exigir(u); if (u.tipo === "Administrador") return; const b = db.barbeiros.find((x) => x.usuarioId === u.id); if (!b || b.id !== id) throw new Erro("Você não tem permissão para isso.", 403); };
const meuBarbeiro = (u) => db.barbeiros.find((x) => x.usuarioId === u.id);

/* ---------- rotas ---------- */
const rotas = [];
const rota = (m, re, fn) => rotas.push([m, new RegExp("^" + re + "$"), fn]);

rota("POST", "/api/usuarios/registrar", ({ body }) => {
  if (!body.nome || body.nome.trim().length < 2) throw new Erro("O nome e obrigatorio.");
  if (!/^\S+@\S+\.\S+$/.test(body.email || "")) throw new Erro("Informe um e-mail valido.");
  if (!body.senha || body.senha.length < 6) throw new Erro("A senha precisa ter pelo menos 6 caracteres.");
  if (db.usuarios.some((u) => u.email === body.email.toLowerCase())) throw new Erro("Ja existe um usuario com esse email.", 409);
  const u = { id: prox(), nome: body.nome.trim(), email: body.email.toLowerCase(), senha: body.senha, tipo: "Cliente", fotoUrl: null };
  db.usuarios.push(u); return auth(u);
});
rota("POST", "/api/usuarios/login", ({ body }) => {
  const u = db.usuarios.find((x) => x.email === String(body.email || "").trim().toLowerCase());
  if (!u || u.senha !== body.senha) throw new Erro("Email ou senha invalidos.", 401);
  const b = meuBarbeiro(u); if (b && !b.ativo) throw new Erro("Sua conta de profissional esta desativada. Fale com o administrador.", 403);
  return auth(u);
});
rota("POST", "/api/usuarios/esqueci-senha", ({ body }) => {
  const u = db.usuarios.find((x) => x.email === String(body.email || "").trim().toLowerCase());
  let linkDev = null;
  if (u) { const t = crypto.randomBytes(12).toString("hex"); db.resets.push({ token: t, usuarioId: u.id }); linkDev = `http://localhost:${PORTA}/redefinir.html?token=${t}`; }
  return { mensagem: "Se o e-mail estiver cadastrado, enviamos um link para redefinir a senha. Ele vale por 30 minutos.", linkDev };
});
rota("POST", "/api/usuarios/redefinir-senha", ({ body }) => {
  if (!body.novaSenha || body.novaSenha.length < 6) throw new Erro("A senha precisa ter pelo menos 6 caracteres.");
  const r = db.resets.find((x) => x.token === body.token);
  if (!r) throw new Erro("Link invalido ou expirado. Peca um novo link.");
  usuario(r.usuarioId).senha = body.novaSenha; db.resets = db.resets.filter((x) => x !== r);
  return { mensagem: "Senha alterada com sucesso. Entre com a nova senha." };
});
rota("GET", "/api/usuarios/me", ({ user }) => { exigir(user); return dtoUsuario(user); });
rota("PUT", "/api/usuarios/me", ({ user, body }) => {
  exigir(user);
  if (!body.nome || body.nome.trim().length < 2) throw new Erro("O nome e obrigatorio.");
  if (!/^\S+@\S+\.\S+$/.test(body.email || "")) throw new Erro("Informe um e-mail valido.");
  if (db.usuarios.some((u) => u.id !== user.id && u.email === body.email.toLowerCase())) throw new Erro("Ja existe um usuario com esse email.", 409);
  user.nome = body.nome.trim(); user.email = body.email.toLowerCase(); return dtoUsuario(user);
});
rota("PUT", "/api/usuarios/me/senha", ({ user, body }) => {
  exigir(user);
  if (body.senhaAtual !== user.senha) throw new Erro("A senha atual esta incorreta.");
  if (!body.novaSenha || body.novaSenha.length < 6) throw new Erro("A senha precisa ter pelo menos 6 caracteres.");
  user.senha = body.novaSenha; return { mensagem: "Senha alterada com sucesso." };
});
rota("POST", "/api/usuarios/me/foto", ({ user }) => { exigir(user); user.fotoUrl = `/uploads/mock/${prox()}.svg`; return dtoUsuario(user); });
rota("DELETE", "/api/usuarios/me/foto", ({ user }) => { exigir(user); user.fotoUrl = null; return dtoUsuario(user); });

rota("GET", "/api/servicos", ({ user, query }) => db.servicos.filter((s) => s.ativo || (query.todos === "true" && user && user.tipo === "Administrador")).sort((a, b) => a.nome.localeCompare(b.nome)));
rota("GET", "/api/servicos/(\\d+)", ({ p }) => { const s = servico(+p[0]); if (!s) throw new Erro("Servico nao encontrado.", 404); return s; });
function validarServ(b) {
  if (!b.nome || b.nome.trim().length < 2) throw new Erro("Informe o nome do servico (ate 150 caracteres).");
  if (b.preco < 0) throw new Erro("Informe um preco valido.");
  if (!(b.duracaoMinutos >= 5 && b.duracaoMinutos <= 480)) throw new Erro("A duracao deve ficar entre 5 e 480 minutos.");
}
rota("POST", "/api/servicos", ({ user, body }) => { exigir(user, "Administrador"); validarServ(body); const s = { id: prox(), nome: body.nome.trim(), preco: body.preco, duracaoMinutos: body.duracaoMinutos, ativo: true }; db.servicos.push(s); return s; });
rota("PUT", "/api/servicos/(\\d+)", ({ user, body, p }) => { exigir(user, "Administrador"); validarServ(body); const s = servico(+p[0]); if (!s) throw new Erro("Servico nao encontrado.", 404); Object.assign(s, { nome: body.nome.trim(), preco: body.preco, duracaoMinutos: body.duracaoMinutos }); return s; });
rota("PATCH", "/api/servicos/(\\d+)/ativo", ({ user, body, p }) => { exigir(user, "Administrador"); const s = servico(+p[0]); if (!s) throw new Erro("Servico nao encontrado.", 404); s.ativo = !!body.ativo; return s; });
rota("DELETE", "/api/servicos/(\\d+)", ({ user, p }) => {
  exigir(user, "Administrador"); const id = +p[0];
  if (db.agendamentos.some((a) => a.servicos.some((s) => s.servicoId === id))) throw new Erro("Este servico ja foi usado em agendamentos. Desative-o em vez de excluir para manter o historico.", 409);
  db.servicos = db.servicos.filter((s) => s.id !== id); return 204;
});

rota("GET", "/api/barbeiros", ({ user, query }) => db.barbeiros.filter((b) => b.ativo || (query.todos === "true" && user && user.tipo === "Administrador")).map((b) => dtoBarbeiro(b, user)));
rota("GET", "/api/barbeiros/(\\d+)/perfil", ({ user, p }) => {
  const b = barbeiro(+p[0]); if (!b) throw new Erro("Profissional nao encontrada.", 404);
  const u = usuario(b.usuarioId); const m = media(b.id);
  return { id: b.id, nome: u.nome, fotoUrl: u.fotoUrl, especialidades: b.especialidades, bio: b.bio, ativo: b.ativo, media: m.media, totalAvaliacoes: m.total, fotos: b.fotos.slice().reverse(),
    avaliacoes: db.avaliacoes.filter((a) => a.barbeiroId === b.id).reverse().map((a) => ({ id: a.id, nomeCliente: usuario(a.clienteId).nome.split(" ")[0], nota: a.nota, comentario: a.comentario, data: a.criadoEm })) };
});
function validarBarb(b, novo) {
  if (!b.nome || b.nome.trim().length < 2) throw new Erro("O nome e obrigatorio.");
  if (!/^\S+@\S+\.\S+$/.test(b.email || "")) throw new Erro("Informe um e-mail valido.");
  if (novo && (!b.senha || b.senha.length < 6)) throw new Erro("A senha precisa ter pelo menos 6 caracteres.");
  if (b.percentualBarbeiro < 0 || b.percentualBarbeiro > 100) throw new Erro("A porcentagem deve estar entre 0 e 100.");
}
rota("POST", "/api/barbeiros", ({ user, body }) => {
  exigir(user, "Administrador"); validarBarb(body, true);
  if (db.usuarios.some((u) => u.email === body.email.toLowerCase())) throw new Erro("Ja existe um usuario com esse email.", 409);
  const u = { id: prox(), nome: body.nome.trim(), email: body.email.toLowerCase(), senha: body.senha, tipo: "Barbeiro", fotoUrl: null }; db.usuarios.push(u);
  const b = { id: prox(), usuarioId: u.id, percentual: body.percentualBarbeiro, ativo: true, especialidades: "", bio: "", fotos: [] }; db.barbeiros.push(b); return dtoBarbeiro(b, user);
});
rota("PUT", "/api/barbeiros/(\\d+)", ({ user, body, p }) => {
  exigir(user, "Administrador"); validarBarb(body, false); const b = barbeiro(+p[0]); if (!b) throw new Erro("Profissional nao encontrada.", 404);
  const u = usuario(b.usuarioId);
  if (db.usuarios.some((x) => x.id !== u.id && x.email === body.email.toLowerCase())) throw new Erro("Ja existe um usuario com esse email.", 409);
  u.nome = body.nome.trim(); u.email = body.email.toLowerCase(); b.percentual = body.percentualBarbeiro; if (body.novaSenha) u.senha = body.novaSenha; return dtoBarbeiro(b, user);
});
rota("PATCH", "/api/barbeiros/(\\d+)/ativo", ({ user, body, p }) => { exigir(user, "Administrador"); const b = barbeiro(+p[0]); if (!b) throw new Erro("Profissional nao encontrada.", 404); b.ativo = !!body.ativo; return { ativo: b.ativo }; });
rota("DELETE", "/api/barbeiros/(\\d+)", ({ user, p }) => {
  exigir(user, "Administrador"); const id = +p[0];
  if (db.agendamentos.some((a) => a.barbeiroId === id)) throw new Erro("Esta profissional tem agendamentos no historico. Desative-a em vez de excluir.", 409);
  const b = barbeiro(id); db.barbeiros = db.barbeiros.filter((x) => x.id !== id); db.usuarios = db.usuarios.filter((u) => u.id !== b.usuarioId); return 204;
});
rota("PUT", "/api/barbeiros/(\\d+)/perfil", ({ user, body, p }) => {
  const id = +p[0]; podeBarbeiro(user, id); const b = barbeiro(id);
  if ((body.especialidades || "").length > 200) throw new Erro("As especialidades podem ter no maximo 200 caracteres.");
  b.especialidades = (body.especialidades || "").trim(); b.bio = (body.bio || "").trim();
  return rotas.find((r) => r[1].test("/api/barbeiros/" + id + "/perfil") && r[0] === "GET")[2]({ user, p: [String(id)] });
});
rota("POST", "/api/barbeiros/(\\d+)/portfolio", ({ user, p, form }) => {
  const id = +p[0]; podeBarbeiro(user, id); const b = barbeiro(id);
  if (b.fotos.length >= 12) throw new Erro("O portfolio pode ter no maximo 12 fotos. Remova alguma para adicionar outra.");
  const f = { id: prox(), url: `/uploads/mock/${prox()}.svg`, legenda: (form && form.legenda) || "" }; b.fotos.push(f); return f;
});
rota("DELETE", "/api/barbeiros/(\\d+)/portfolio/(\\d+)", ({ user, p }) => { const id = +p[0]; podeBarbeiro(user, id); const b = barbeiro(id); b.fotos = b.fotos.filter((f) => f.id !== +p[1]); return 204; });
rota("GET", "/api/barbeiros/(\\d+)/horarios", ({ user, p }) => { podeBarbeiro(user, +p[0]); return horariosDe(+p[0]); });
rota("PUT", "/api/barbeiros/(\\d+)/horarios", ({ user, body, p }) => {
  const id = +p[0]; podeBarbeiro(user, id);
  if (!Array.isArray(body) || !body.length) throw new Erro("Envie os horarios dos dias da semana.");
  const novos = [];
  for (let d = 0; d <= 6; d++) {
    const x = body.find((h) => h.diaSemana === d) || { diaSemana: d, trabalha: false, inicio: "09:00", fim: "18:00" };
    if (x.trabalha && x.inicio >= x.fim) throw new Erro("O inicio do expediente deve ser antes do fim.");
    novos.push({ diaSemana: d, trabalha: !!x.trabalha, inicio: x.inicio || "09:00", fim: x.fim || "18:00", almocoInicio: x.trabalha ? x.almocoInicio || null : null, almocoFim: x.trabalha ? x.almocoFim || null : null });
  }
  db.horarios[id] = novos; return novos;
});
rota("GET", "/api/barbeiros/(\\d+)/folgas", ({ user, p }) => { podeBarbeiro(user, +p[0]); return db.folgas.filter((f) => f.barbeiroId === +p[0] && ler(f.dataFim) >= dia(agora())); });
rota("POST", "/api/barbeiros/(\\d+)/folgas", ({ user, body, p }) => {
  const id = +p[0]; podeBarbeiro(user, id);
  const i = dia(ler(body.dataInicio)); const f = dia(ler(body.dataFim));
  if (f < i) throw new Erro("A data final nao pode ser antes da data inicial.");
  if (f < dia(agora())) throw new Erro("Escolha datas a partir de hoje.");
  const folga = { id: prox(), barbeiroId: id, dataInicio: isoData(i) + "T00:00:00", dataFim: isoData(f) + "T00:00:00", motivo: body.motivo || "" };
  db.folgas.push(folga);
  const afetados = db.agendamentos.filter((a) => a.barbeiroId === id && a.status === "Agendado" && ler(a.dataHora) >= i && ler(a.dataHora) < somar(f, 1)).length;
  return { folga: { id: folga.id, dataInicio: folga.dataInicio, dataFim: folga.dataFim, motivo: folga.motivo }, agendamentosAfetados: afetados };
});
rota("DELETE", "/api/barbeiros/(\\d+)/folgas/(\\d+)", ({ user, p }) => { podeBarbeiro(user, +p[0]); db.folgas = db.folgas.filter((f) => f.id !== +p[1]); return 204; });
rota("GET", "/api/barbeiros/(\\d+)/ganhos", ({ user, p, query }) => {
  const id = +p[0]; podeBarbeiro(user, id);
  const mes = query.mes || isoData(agora()).substring(0, 7);
  const l = db.agendamentos.filter((a) => a.barbeiroId === id && a.dataHora.startsWith(mes));
  const soma = (st) => r2(l.filter((a) => a.status === st).reduce((s, a) => s + a.valorBarbeiro, 0));
  return { mes, concluidos: l.filter((a) => a.status === "Concluido").length, faltas: l.filter((a) => a.status === "Faltou").length, cancelados: l.filter((a) => a.status === "Cancelado").length, agendados: l.filter((a) => a.status === "Agendado").length, totalRecebido: soma("Concluido"), totalPrevisto: soma("Agendado") };
});

rota("GET", "/api/agendamentos", ({ user, query }) => {
  exigir(user);
  let l = db.agendamentos.slice();
  if (user.tipo === "Cliente") l = l.filter((a) => a.clienteId === user.id);
  else if (user.tipo === "Barbeiro") l = l.filter((a) => a.barbeiroId === meuBarbeiro(user).id);
  else if (query.barbeiroId) l = l.filter((a) => a.barbeiroId === +query.barbeiroId);
  if (query.de) l = l.filter((a) => ler(a.dataHora) >= ler(query.de));
  if (query.ate) l = l.filter((a) => ler(a.dataHora) < ler(query.ate));
  if (query.status) l = l.filter((a) => a.status === query.status);
  return l.sort((a, b) => a.dataHora.localeCompare(b.dataHora)).map(dtoAg);
});
rota("GET", "/api/agendamentos/horarios-livres", ({ user, query }) => {
  exigir(user);
  let dur = Number(query.duracao || 0);
  if (dur <= 0) dur = String(query.servicoIds || "").split(",").filter(Boolean).reduce((s, id) => s + ((servico(+id) || {}).duracaoMinutos || 0), 0);
  if (dur <= 0) throw new Erro("Informe os servicos (servicoIds) ou a duracao em minutos.");
  return slots(+query.barbeiroId, query.data, dur, query.ignorarAgendamentoId ? +query.ignorarAgendamentoId : null);
});
rota("GET", "/api/agendamentos/(\\d+)", ({ user, p }) => {
  exigir(user); const a = db.agendamentos.find((x) => x.id === +p[0]);
  if (!a || (user.tipo === "Cliente" && a.clienteId !== user.id) || (user.tipo === "Barbeiro" && a.barbeiroId !== meuBarbeiro(user).id)) throw new Erro("Agendamento nao encontrado.", 404);
  return dtoAg(a);
});
function calcDesc(clienteId, total, codigo, usarFid) {
  const r = { valor: 0, cupom: null, fid: false };
  if (codigo && usarFid) throw new Erro("Use um cupom OU o servico gratis da fidelidade, nao os dois ao mesmo tempo.");
  if (usarFid) { if (fidelidade(clienteId).gratisDisponiveis <= 0) throw new Erro("Voce ainda nao tem um servico gratis disponivel."); r.valor = total; r.fid = true; }
  else if (codigo) { const x = aplicarCupom(codigo, total); r.cupom = x.cupom; r.valor = x.desconto; }
  return r;
}
rota("POST", "/api/agendamentos/simular", ({ user, body }) => {
  exigir(user, "Cliente", "Administrador");
  const l = listaServicos(body.servicoIds); const total = l.reduce((s, x) => s + x.preco, 0); const dur = l.reduce((s, x) => s + x.duracaoMinutos, 0);
  const r = { valorServico: total, valorDesconto: 0, valorFinal: total, duracaoMinutos: dur, cupomCodigo: null, usouFidelidade: false, aviso: null };
  if (user.tipo === "Cliente") { try { const d = calcDesc(user.id, total, body.cupomCodigo, body.usarFidelidade); r.valorDesconto = d.valor; r.valorFinal = total - d.valor; r.cupomCodigo = d.cupom ? d.cupom.codigo : null; r.usouFidelidade = d.fid; } catch (e) { r.aviso = e.message; } }
  return r;
});
rota("POST", "/api/agendamentos", ({ user, body }) => {
  exigir(user, "Cliente", "Administrador");
  const clienteId = user.tipo === "Administrador" ? body.clienteId : user.id;
  if (!clienteId) throw new Erro("Informe o cliente.");
  const ini = ler(body.dataHora); if (ini <= agora()) throw new Erro("A data do agendamento nao pode estar no passado.");
  const l = listaServicos(body.servicoIds); const b = barbeiro(body.barbeiroId); if (!b) throw new Erro("Profissional nao encontrada.", 404); if (!b.ativo) throw new Erro("Esta profissional nao esta disponivel no momento.");
  const dur = l.reduce((s, x) => s + x.duracaoMinutos, 0); const total = l.reduce((s, x) => s + x.preco, 0);
  if (!slots(b.id, isoData(ini), dur, null).includes(`${dois(ini.getHours())}:${dois(ini.getMinutes())}`)) throw new Erro("Esse horario nao esta disponivel com esta profissional. Escolha outro horario.", 409);
  const d = calcDesc(clienteId, total, body.cupomCodigo, body.usarFidelidade);
  if (d.cupom) d.cupom.usosAtuais++;
  const a = criarAg({ clienteId, barbeiroId: b.id, servicoIds: l.map((x) => x.id), data: ini, cupomCodigo: d.cupom ? d.cupom.codigo : null, fidel: d.fid, desconto: d.valor });
  notificar(b.usuarioId, "Novo agendamento", `${usuario(clienteId).nome} agendou ${l.map((x) => x.nome).join(" + ")} para ${quando(ini)}.`);
  return dtoAg(a);
});
const achar = (id) => { const a = db.agendamentos.find((x) => x.id === id); if (!a) throw new Erro("Agendamento nao encontrado.", 404); return a; };
rota("PUT", "/api/agendamentos/(\\d+)/reagendar", ({ user, body, p }) => {
  exigir(user); const a = achar(+p[0]);
  if (!(user.tipo === "Administrador" || (user.tipo === "Cliente" && a.clienteId === user.id))) throw new Erro("Agendamento nao encontrado.", 404);
  if (a.status !== "Agendado") throw new Erro("Apenas agendamentos ativos podem ser reagendados.");
  if (ler(a.dataHora) <= agora()) throw new Erro("Este agendamento ja passou e nao pode ser reagendado.");
  const nova = ler(body.dataHora); if (nova <= agora()) throw new Erro("A nova data nao pode estar no passado.");
  const bid = body.barbeiroId || a.barbeiroId; const b = barbeiro(bid); if (!b || !b.ativo) throw new Erro("Esta profissional nao esta disponivel.");
  if (!slots(bid, isoData(nova), a.duracaoMinutos, a.id).includes(`${dois(nova.getHours())}:${dois(nova.getMinutes())}`)) throw new Erro("Esse horario nao esta disponivel com esta profissional. Escolha outro horario.", 409);
  const antigaData = ler(a.dataHora), antigoB = a.barbeiroId;
  if (bid !== a.barbeiroId) { a.barbeiroId = bid; a.percentualBarbeiro = b.percentual; a.valorBarbeiro = r2((a.valorServico * b.percentual) / 100); a.valorAdministrador = r2(a.valorFinal - a.valorBarbeiro); }
  a.dataHora = iso(nova); notificar(b.usuarioId, "Agendamento reagendado", `${usuario(a.clienteId).nome} agendou para ${quando(nova)} (antes: ${quando(antigaData)}).`);
  liberarEspera(antigoB, antigaData); return dtoAg(a);
});
rota("PUT", "/api/agendamentos/(\\d+)/cancelar", ({ user, p }) => {
  exigir(user); const a = achar(+p[0]); const b = barbeiro(a.barbeiroId);
  const dono = user.tipo === "Cliente" && a.clienteId === user.id; const meu = user.tipo === "Barbeiro" && b.usuarioId === user.id;
  if (!(user.tipo === "Administrador" || dono || meu)) throw new Erro("Agendamento nao encontrado.", 404);
  if (a.status !== "Agendado") throw new Erro("Este agendamento nao pode mais ser cancelado.");
  if (dono && ler(a.dataHora) <= agora()) throw new Erro("Nao e possivel cancelar um agendamento que ja passou.");
  a.status = "Cancelado";
  if (a.cupomCodigo) { const c = db.cupons.find((x) => x.codigo === a.cupomCodigo); if (c && c.usosAtuais > 0) c.usosAtuais--; }
  if (dono) notificar(b.usuarioId, "Agendamento cancelado", `${usuario(a.clienteId).nome} cancelou o horario de ${quando(ler(a.dataHora))}.`);
  else notificar(a.clienteId, "Agendamento cancelado", `Seu horario de ${quando(ler(a.dataHora))} foi cancelado pelo salao.`);
  if (ler(a.dataHora) > agora()) liberarEspera(a.barbeiroId, ler(a.dataHora)); return dtoAg(a);
});
rota("PUT", "/api/agendamentos/(\\d+)/status", ({ user, body, p }) => {
  exigir(user, "Barbeiro", "Administrador"); const a = achar(+p[0]); const b = barbeiro(a.barbeiroId);
  if (user.tipo === "Barbeiro" && b.usuarioId !== user.id) throw new Erro("Agendamento nao encontrado.", 404);
  if (!["Concluido", "Faltou"].includes(body.status)) throw new Erro("Status invalido. Use Concluido ou Faltou.");
  if (a.status !== "Agendado") throw new Erro("Este agendamento ja esta " + a.status + ".");
  if (body.status === "Faltou" && ler(a.dataHora) > agora()) throw new Erro("So e possivel registrar falta depois do horario do agendamento.");
  a.status = body.status;
  if (body.status === "Concluido") notificar(a.clienteId, "Atendimento concluido", 'Conte como foi! Avalie seu atendimento em "Meus agendamentos".');
  return dtoAg(a);
});
rota("DELETE", "/api/agendamentos/(\\d+)", ({ user, p }) => { exigir(user, "Administrador"); achar(+p[0]); db.agendamentos = db.agendamentos.filter((a) => a.id !== +p[0]); return 204; });

rota("POST", "/api/avaliacoes", ({ user, body }) => {
  exigir(user, "Cliente"); if (!(body.nota >= 1 && body.nota <= 5)) throw new Erro("A nota deve ser de 1 a 5 estrelas.");
  const a = db.agendamentos.find((x) => x.id === body.agendamentoId && x.clienteId === user.id); if (!a) throw new Erro("Agendamento nao encontrado.", 404);
  if (a.status !== "Concluido") throw new Erro("Voce so pode avaliar atendimentos concluidos.");
  if (a.avaliacao) throw new Erro("Este atendimento ja foi avaliado.", 409);
  a.avaliacao = { nota: body.nota, comentario: (body.comentario || "").trim() };
  db.avaliacoes.push({ id: prox(), agendamentoId: a.id, barbeiroId: a.barbeiroId, clienteId: user.id, ...a.avaliacao, criadoEm: iso(agora()) });
  notificar(barbeiro(a.barbeiroId).usuarioId, "Nova avaliacao", `${user.nome} avaliou seu atendimento com ${body.nota} estrela(s).`); return a.avaliacao;
});
const favs = (u) => db.favoritos.filter((f) => f.clienteId === u.id).map((f) => f.barbeiroId);
rota("GET", "/api/favoritos", ({ user }) => { exigir(user, "Cliente"); return favs(user); });
rota("POST", "/api/favoritos/(\\d+)", ({ user, p }) => { exigir(user, "Cliente"); if (!barbeiro(+p[0])) throw new Erro("Profissional nao encontrada.", 404); if (!favs(user).includes(+p[0])) db.favoritos.push({ clienteId: user.id, barbeiroId: +p[0] }); return favs(user); });
rota("DELETE", "/api/favoritos/(\\d+)", ({ user, p }) => { exigir(user, "Cliente"); db.favoritos = db.favoritos.filter((f) => !(f.clienteId === user.id && f.barbeiroId === +p[0])); return favs(user); });
const dtoEspera = (e) => ({ id: e.id, barbeiroId: e.barbeiroId, nomeBarbeiro: usuario(barbeiro(e.barbeiroId).usuarioId).nome, clienteId: e.clienteId, nomeCliente: usuario(e.clienteId).nome, data: e.data + "T00:00:00", servicos: e.servicoIds.map((i) => (servico(i) || {}).nome).filter(Boolean).join(" + "), servicoIds: e.servicoIds, status: e.status });
rota("GET", "/api/lista-espera", ({ user }) => { exigir(user, "Cliente", "Administrador"); return db.espera.filter((e) => user.tipo === "Administrador" || e.clienteId === user.id).map(dtoEspera); });
rota("POST", "/api/lista-espera", ({ user, body }) => {
  exigir(user, "Cliente"); const d = dia(ler(body.data)); if (d < dia(agora())) throw new Erro("Escolha uma data a partir de hoje.");
  if (!barbeiro(body.barbeiroId)) throw new Erro("Profissional nao encontrada.", 404);
  if (db.espera.some((e) => e.clienteId === user.id && e.barbeiroId === body.barbeiroId && e.data === isoData(d) && e.status === "Aguardando")) throw new Erro("Voce ja esta na lista de espera dessa profissional para esse dia.", 409);
  const e = { id: prox(), clienteId: user.id, barbeiroId: body.barbeiroId, data: isoData(d), servicoIds: body.servicoIds || [], status: "Aguardando" }; db.espera.push(e); return dtoEspera(e);
});
rota("DELETE", "/api/lista-espera/(\\d+)", ({ user, p }) => { exigir(user, "Cliente", "Administrador"); db.espera = db.espera.filter((e) => !(e.id === +p[0] && (user.tipo === "Administrador" || e.clienteId === user.id))); return 204; });
rota("GET", "/api/fidelidade", ({ user }) => { exigir(user, "Cliente"); return fidelidade(user.id); });

rota("GET", "/api/cupons/validar", ({ user, query }) => {
  exigir(user);
  try { const x = aplicarCupom(query.codigo, Number(query.valor || 0)); return { valido: true, mensagem: "Cupom aplicado!", codigo: x.cupom.codigo, tipo: x.cupom.tipo, valor: x.cupom.valor, desconto: x.desconto }; }
  catch (e) { return { valido: false, mensagem: e.message, codigo: null, tipo: null, valor: 0, desconto: 0 }; }
});
rota("GET", "/api/cupons", ({ user }) => { exigir(user, "Administrador"); return db.cupons.slice().reverse(); });
function validarCupom(b) {
  const cod = String(b.codigo || "").trim().toUpperCase();
  if (!/^[A-Z0-9_-]{3,40}$/.test(cod)) throw new Erro("O codigo deve ter de 3 a 40 caracteres (letras, numeros, - ou _).");
  if (!["Percentual", "Valor"].includes(b.tipo)) throw new Erro("O tipo do cupom deve ser Percentual ou Valor.");
  if (!(b.valor > 0)) throw new Erro("O valor do desconto deve ser maior que zero.");
  if (b.tipo === "Percentual" && b.valor > 100) throw new Erro("O desconto percentual nao pode passar de 100%.");
  return cod;
}
rota("POST", "/api/cupons", ({ user, body }) => { exigir(user, "Administrador"); const cod = validarCupom(body); if (db.cupons.some((c) => c.codigo === cod)) throw new Erro("Ja existe um cupom com esse codigo.", 409); const c = { id: prox(), codigo: cod, tipo: body.tipo, valor: body.valor, validade: body.validade ? String(body.validade).substring(0, 10) + "T00:00:00" : null, usosMaximos: body.usosMaximos || null, usosAtuais: 0, ativo: true }; db.cupons.push(c); return c; });
rota("PUT", "/api/cupons/(\\d+)", ({ user, body, p }) => { exigir(user, "Administrador"); const cod = validarCupom(body); const c = db.cupons.find((x) => x.id === +p[0]); if (!c) throw new Erro("Cupom nao encontrado.", 404); Object.assign(c, { codigo: cod, tipo: body.tipo, valor: body.valor, validade: body.validade ? String(body.validade).substring(0, 10) + "T00:00:00" : null, usosMaximos: body.usosMaximos || null }); return c; });
rota("PATCH", "/api/cupons/(\\d+)/ativo", ({ user, body, p }) => { exigir(user, "Administrador"); const c = db.cupons.find((x) => x.id === +p[0]); if (!c) throw new Erro("Cupom nao encontrado.", 404); c.ativo = !!body.ativo; return c; });
rota("DELETE", "/api/cupons/(\\d+)", ({ user, p }) => { exigir(user, "Administrador"); const c = db.cupons.find((x) => x.id === +p[0]); if (c && c.usosAtuais > 0) throw new Erro("Este cupom ja foi usado em agendamentos. Desative-o em vez de excluir.", 409); db.cupons = db.cupons.filter((x) => x.id !== +p[0]); return 204; });

const notifs = (u) => { const l = db.notificacoes.filter((n) => n.usuarioId === u.id).sort((a, b) => b.criadoEm.localeCompare(a.criadoEm)).slice(0, 30); return { naoLidas: db.notificacoes.filter((n) => n.usuarioId === u.id && !n.lida).length, itens: l.map(({ id, titulo, mensagem, lida, criadoEm }) => ({ id, titulo, mensagem, lida, criadoEm })) }; };
rota("GET", "/api/notificacoes", ({ user }) => { exigir(user); return notifs(user); });
rota("PUT", "/api/notificacoes/lidas", ({ user }) => { exigir(user); db.notificacoes.filter((n) => n.usuarioId === user.id).forEach((n) => (n.lida = true)); return notifs(user); });
rota("PUT", "/api/notificacoes/(\\d+)/lida", ({ user, p }) => { exigir(user); const n = db.notificacoes.find((x) => x.id === +p[0] && x.usuarioId === user.id); if (n) n.lida = true; return notifs(user); });

rota("GET", "/api/clientes", ({ user }) => { exigir(user, "Administrador"); return db.usuarios.filter((u) => u.tipo === "Cliente").map((u) => ({ id: u.id, nome: u.nome, email: u.email, fotoUrl: u.fotoUrl, totalAgendamentos: db.agendamentos.filter((a) => a.clienteId === u.id).length })); });
rota("DELETE", "/api/clientes/(\\d+)", ({ user, p }) => { exigir(user, "Administrador"); if (db.agendamentos.some((a) => a.clienteId === +p[0])) throw new Erro("Este cliente tem agendamentos no historico e nao pode ser excluido.", 409); db.usuarios = db.usuarios.filter((u) => u.id !== +p[0]); return 204; });

rota("GET", "/api/dashboard", ({ user, query }) => {
  exigir(user, "Administrador"); const meses = Math.min(24, Math.max(1, +(query.meses || 6)));
  const hoje = dia(agora()); const inicioMes = new Date(hoje.getFullYear(), hoje.getMonth(), 1); const desde = new Date(inicioMes.getFullYear(), inicioMes.getMonth() - (meses - 1), 1);
  const periodo = db.agendamentos.filter((a) => ler(a.dataHora) >= desde && a.status !== "Cancelado"); const conc = periodo.filter((a) => a.status === "Concluido");
  const mesDe = (d) => `${d.getFullYear()}-${dois(d.getMonth() + 1)}`; const mAtual = mesDe(inicioMes); const mAnt = mesDe(new Date(inicioMes.getFullYear(), inicioMes.getMonth() - 1, 1));
  const doMes = conc.filter((a) => a.dataHora.startsWith(mAtual)); const fat = doMes.reduce((s, a) => s + a.valorFinal, 0);
  const fatMes = []; for (let i = 0; i < meses; i++) { const d = new Date(desde.getFullYear(), desde.getMonth() + i, 1); const m = mesDe(d); const l = conc.filter((a) => a.dataHora.startsWith(m)); fatMes.push({ mes: m, faturamento: l.reduce((s, a) => s + a.valorFinal, 0), atendimentos: l.length }); }
  const cont = {}; periodo.forEach((a) => a.servicos.forEach((s) => { cont[s.nome] = cont[s.nome] || { nome: s.nome, quantidade: 0, faturamento: 0 }; cont[s.nome].quantidade++; cont[s.nome].faturamento += s.preco; }));
  const pico = []; for (let h = 7; h <= 21; h++) pico.push({ hora: h, quantidade: periodo.filter((a) => ler(a.dataHora).getHours() === h).length });
  return { faturamentoMes: fat, faturamentoMesAnterior: conc.filter((a) => a.dataHora.startsWith(mAnt)).reduce((s, a) => s + a.valorFinal, 0), atendimentosMes: doMes.length, ticketMedio: doMes.length ? r2(fat / doMes.length) : 0,
    agendamentosHoje: periodo.filter((a) => a.dataHora.startsWith(isoData(hoje))).length, agendamentosFuturos: periodo.filter((a) => a.status === "Agendado" && ler(a.dataHora) > agora()).length, totalClientes: db.usuarios.filter((u) => u.tipo === "Cliente").length, faltas: db.agendamentos.filter((a) => a.status === "Faltou" && a.dataHora.startsWith(mAtual)).length,
    faturamentoPorMes: fatMes, servicosMaisPedidos: Object.values(cont).sort((a, b) => b.quantidade - a.quantidade).slice(0, 8),
    rankingBarbeiros: db.barbeiros.map((b) => { const l = conc.filter((a) => a.barbeiroId === b.id); return { barbeiroId: b.id, nome: usuario(b.usuarioId).nome, fotoUrl: usuario(b.usuarioId).fotoUrl, atendimentos: l.length, faturamento: l.reduce((s, a) => s + a.valorFinal, 0), comissao: l.reduce((s, a) => s + a.valorBarbeiro, 0), media: media(b.id).media }; }).sort((a, b) => b.faturamento - a.faturamento), horariosPico: pico };
});

/* ---------- servidor HTTP ---------- */
const tipos = { ".html": "text/html; charset=utf-8", ".js": "application/javascript; charset=utf-8", ".css": "text/css; charset=utf-8", ".svg": "image/svg+xml", ".png": "image/png", ".jpg": "image/jpeg", ".webp": "image/webp", ".json": "application/json" };

function responder(res, status, dados) {
  if (status === 204) { res.writeHead(204); return res.end(); }
  const corpo = JSON.stringify(dados);
  res.writeHead(status, { "Content-Type": "application/json; charset=utf-8" }); res.end(corpo);
}

http.createServer((req, res) => {
  const url = new URL(req.url, "http://x"); const caminho = decodeURIComponent(url.pathname);
  const chunks = []; req.on("data", (c) => chunks.push(c));
  req.on("end", () => {
    if (caminho.startsWith("/api/")) {
      let user = null; const h = req.headers.authorization || "";
      if (h.startsWith("Bearer mock.")) { try { user = usuario(JSON.parse(Buffer.from(h.substring(12), "base64").toString()).id) || null; } catch (e) { user = null; } }
      const bruto = Buffer.concat(chunks); let body = {}; let form = null;
      const ct = req.headers["content-type"] || "";
      if (ct.includes("application/json") && bruto.length) { try { body = JSON.parse(bruto.toString()); } catch (e) { return responder(res, 400, { mensagem: "Dados invalidos. Confira os campos e tente novamente." }); } }
      else if (ct.includes("multipart/form-data")) { form = {}; const m = bruto.toString("latin1").match(/name="legenda"\r\n\r\n([^\r]*)/); if (m) form.legenda = Buffer.from(m[1], "latin1").toString("utf8"); }
      const query = Object.fromEntries(url.searchParams.entries());
      for (const [m, re, fn] of rotas) {
        if (m !== req.method) continue; const mt = re.exec(caminho); if (!mt) continue;
        try { const r = fn({ user, body, form, query, p: mt.slice(1) }); return responder(res, r === 204 ? 204 : 200, r); }
        catch (e) { if (e instanceof Erro) return responder(res, e.status, { mensagem: e.message }); console.error(e); return responder(res, 500, { mensagem: "Erro interno do servidor." }); }
      }
      return responder(res, 404, { mensagem: "Rota nao encontrada: " + req.method + " " + caminho });
    }
    if (caminho.startsWith("/uploads/mock/")) {
      const n = parseInt(caminho.replace(/\D/g, ""), 10) || 1; const cor = ["#c8a15c", "#6b3a4d", "#2e7d4f", "#3b6ea8", "#b1495a"][n % 5];
      res.writeHead(200, { "Content-Type": "image/svg+xml" });
      return res.end(`<svg xmlns="http://www.w3.org/2000/svg" width="400" height="400"><rect width="400" height="400" fill="${cor}"/><circle cx="200" cy="160" r="70" fill="rgba(255,255,255,.55)"/><rect x="90" y="250" width="220" height="150" rx="80" fill="rgba(255,255,255,.55)"/><text x="200" y="385" font-size="28" text-anchor="middle" fill="#fff" font-family="Arial">foto ${n}</text></svg>`);
    }
    let arq = path.join(RAIZ, caminho === "/" ? "index.html" : caminho);
    if (!arq.startsWith(RAIZ) || !fs.existsSync(arq) || fs.statSync(arq).isDirectory()) { res.writeHead(404, { "Content-Type": "text/plain" }); return res.end("404"); }
    res.writeHead(200, { "Content-Type": tipos[path.extname(arq)] || "application/octet-stream", "Cache-Control": "no-store" }); fs.createReadStream(arq).pipe(res);
  });
}).listen(PORTA, () => console.log("Mock rodando em http://localhost:" + PORTA));
