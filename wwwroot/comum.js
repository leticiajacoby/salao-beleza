/* =====================================================================
   comum.js - funcoes compartilhadas por todas as telas do Spa Delas.
   Carregar com: <script src="comum.js"></script> (antes do script da pagina)
   ===================================================================== */

/* ---------- Utilitarios de texto/formatacao ---------- */

function esc(texto) {
    return String(texto === null || texto === undefined ? "" : texto)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function moeda(valor) {
    return Number(valor || 0).toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

function paraData(valor) {
    if (valor instanceof Date) return valor;
    return new Date(valor);
}

function dataBR(valor) {
    return paraData(valor).toLocaleDateString("pt-BR");
}

function horaBR(valor) {
    return paraData(valor).toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
}

function dataHoraBR(valor) {
    return dataBR(valor) + " " + horaBR(valor);
}

function diaSemanaBR(valor) {
    return paraData(valor).toLocaleDateString("pt-BR", { weekday: "short" }).replace(".", "");
}

function duracaoTexto(minutos) {
    minutos = Number(minutos || 0);
    if (minutos < 60) return minutos + " min";
    var h = Math.floor(minutos / 60);
    var m = minutos % 60;
    return m === 0 ? h + "h" : h + "h" + String(m).padStart(2, "0");
}

function dois(n) {
    return String(n).padStart(2, "0");
}

/* "2026-10-20" (data local, sem fuso) */
function isoData(d) {
    d = paraData(d);
    return d.getFullYear() + "-" + dois(d.getMonth() + 1) + "-" + dois(d.getDate());
}

/* "2026-10-20T14:30:00" (sem fuso - o servidor trabalha em horario local) */
function isoDataHora(d) {
    d = paraData(d);
    return isoData(d) + "T" + dois(d.getHours()) + ":" + dois(d.getMinutes()) + ":00";
}

/* "2026-10-20" -> Date local meia-noite */
function lerIsoData(texto) {
    var p = String(texto).substring(0, 10).split("-");
    return new Date(Number(p[0]), Number(p[1]) - 1, Number(p[2]));
}

function somarDias(d, dias) {
    var r = new Date(paraData(d).getTime());
    r.setDate(r.getDate() + dias);
    return r;
}

function mesAnoBR(isoMes) {
    // "2026-10" -> "out/2026"
    var p = String(isoMes).split("-");
    var d = new Date(Number(p[0]), Number(p[1]) - 1, 1);
    return d.toLocaleDateString("pt-BR", { month: "short", year: "2-digit" }).replace(".", "");
}

function badgeStatus(status) {
    var nomes = { Agendado: "Agendado", Concluido: "Concluído", Faltou: "Faltou", Cancelado: "Cancelado" };
    return '<span class="badge badge-' + esc(String(status).toLowerCase()) + '">' + esc(nomes[status] || status) + "</span>";
}

function estrelasHtml(nota) {
    var n = Math.round(Number(nota) || 0);
    var html = "";
    for (var i = 1; i <= 5; i++) {
        html += '<span class="' + (i <= n ? "cheia" : "") + '">★</span>';
    }
    return '<span class="estrelas" title="' + esc(nota) + ' de 5">' + html + "</span>";
}

/* "★ 4,8 (12)" ou "Sem avaliações" */
function mediaHtml(media, total) {
    if (!total) return '<span class="texto-suave texto-pequeno">Sem avaliações</span>';
    return estrelasHtml(media) + ' <span class="texto-pequeno"><b>' + Number(media).toFixed(1).replace(".", ",") + "</b> (" + total + ")</span>";
}

function iniciais(nome) {
    var partes = String(nome || "?").trim().split(/\s+/);
    var a = partes[0] ? partes[0][0] : "?";
    var b = partes.length > 1 ? partes[partes.length - 1][0] : "";
    return (a + b).toUpperCase();
}

function avatarHtml(fotoUrl, nome, tamanho) {
    tamanho = tamanho || 40;
    var conteudo = '<img src="' + (fotoUrl ? esc(fotoUrl) : "imagens/perfil.png") + '" alt="' + esc(nome) + '">';
    return '<span class="av" style="--s:' + tamanho + 'px">' + conteudo + "</span>";
}

/* ---------- Sessao ---------- */

var Sessao = {
    obter: function () {
        try {
            var bruto = localStorage.getItem("sessao");
            if (!bruto) return null;
            var s = JSON.parse(bruto);
            return s && s.token && s.usuario ? s : null;
        } catch (e) {
            return null;
        }
    },
    salvar: function (auth) {
        localStorage.setItem("sessao", JSON.stringify({ token: auth.token, usuario: auth.usuario }));
    },
    atualizarUsuario: function (usuario) {
        var s = Sessao.obter();
        if (!s) return;
        s.usuario = usuario;
        localStorage.setItem("sessao", JSON.stringify(s));
        document.dispatchEvent(new CustomEvent("usuario-atualizado", { detail: usuario }));
    },
    limpar: function () {
        localStorage.removeItem("sessao");
        localStorage.removeItem("usuario"); // formato antigo
    }
};

function paginaInicialDo(tipo) {
    if (tipo === "Administrador") return "admin.html";
    if (tipo === "Barbeiro") return "barbeiro.html";
    return "cliente.html";
}

function sair() {
    Sessao.limpar();
    window.location.href = "index.html";
}

/* Garante que ha alguem logado e do tipo certo. Retorna o usuario (ou null se redirecionou). */
function exigirLogin(tiposPermitidos) {
    var s = Sessao.obter();
    if (!s) {
        window.location.href = "index.html";
        return null;
    }
    if (tiposPermitidos && tiposPermitidos.indexOf(s.usuario.tipo) === -1) {
        window.location.href = paginaInicialDo(s.usuario.tipo);
        return null;
    }
    return s.usuario;
}

/* ---------- Chamadas a API ---------- */

/*
 api("/api/servicos")
 api("/api/servicos", { method: "POST", body: { nome: "Trabalho", preco: 40, duracaoMinutos: 30 } })
 api("/api/usuarios/me/foto", { method: "POST", body: formData })   // FormData e enviado como multipart
 api("/api/usuarios/login", { method: "POST", body: {...}, semAuth: true })
 Retorna o JSON (ou null em 204). Em erro lanca Error com .message em portugues e .status.
*/
async function api(url, opcoes) {
    opcoes = opcoes || {};
    var cabecalhos = {};
    var corpo;

    var s = Sessao.obter();
    if (s && !opcoes.semAuth) cabecalhos["Authorization"] = "Bearer " + s.token;

    if (opcoes.body instanceof FormData) {
        corpo = opcoes.body;
    } else if (opcoes.body !== undefined && opcoes.body !== null) {
        cabecalhos["Content-Type"] = "application/json";
        corpo = JSON.stringify(opcoes.body);
    }

    var resposta;
    try {
        resposta = await fetch(url, { method: opcoes.method || "GET", headers: cabecalhos, body: corpo });
    } catch (e) {
        var falha = new Error("Não foi possível conectar ao servidor. Verifique se a API está rodando.");
        falha.status = 0;
        throw falha;
    }

    var texto = await resposta.text();
    var dados = null;
    if (texto) {
        try { dados = JSON.parse(texto); } catch (e) { dados = null; }
    }

    if (!resposta.ok) {
        if (resposta.status === 401 && !opcoes.semAuth) {
            Sessao.limpar();
            window.location.href = "index.html?expirou=1";
        }
        var mensagem = (dados && (dados.mensagem || dados.title)) || "";
        if (!mensagem) {
            if (resposta.status === 401) mensagem = "Sua sessão expirou. Entre novamente.";
            else if (resposta.status === 403) mensagem = "Você não tem permissão para isso.";
            else if (resposta.status === 404) mensagem = "Não encontrado.";
            else mensagem = "Não foi possível concluir a operação (erro " + resposta.status + ").";
        }
        var erro = new Error(mensagem);
        erro.status = resposta.status;
        throw erro;
    }

    return dados;
}

/* Atalho: executa uma acao mostrando o erro como toast. Retorna o resultado ou undefined em erro. */
async function tentar(funcao) {
    try {
        return await funcao();
    } catch (e) {
        toast(e.message, "erro");
        return undefined;
    }
}

/* ---------- Toasts e modais ---------- */

function toast(mensagem, tipo) {
    var caixa = document.getElementById("toasts");
    if (!caixa) {
        caixa = document.createElement("div");
        caixa.id = "toasts";
        document.body.appendChild(caixa);
    }
    var el = document.createElement("div");
    el.className = "toast " + (tipo || "info");
    el.textContent = mensagem;
    caixa.appendChild(el);
    setTimeout(function () {
        el.style.opacity = "0";
        el.style.transition = "opacity .3s";
        setTimeout(function () { el.remove(); }, 320);
    }, tipo === "erro" ? 6000 : 3500);
}

/*
 const m = abrirModal({ titulo: "Reagendar", html: "<div>...</div>", largo: false,
                        botoes: [{ rotulo: "Cancelar", classe: "btn-secundario", fechar: true },
                                 { rotulo: "Salvar", classe: "btn-primario", aoClicar: async (m) => { ...; m.fechar(); } }] });
 m.corpo  -> elemento do corpo (use m.corpo.querySelector(...))
 m.fechar()
*/
function abrirModal(opcoes) {
    var fundo = document.createElement("div");
    fundo.className = "modal-fundo";

    var caixa = document.createElement("div");
    caixa.className = "modal" + (opcoes.largo ? " largo" : "");
    caixa.setAttribute("role", "dialog");

    var topo = document.createElement("div");
    topo.className = "modal-topo";
    topo.innerHTML = "<h3>" + esc(opcoes.titulo || "") + '</h3><button class="modal-fechar" type="button" aria-label="Fechar">×</button>';

    var corpo = document.createElement("div");
    corpo.className = "modal-corpo";
    corpo.innerHTML = opcoes.html || "";

    var rodape = document.createElement("div");
    rodape.className = "modal-rodape";

    caixa.appendChild(topo);
    caixa.appendChild(corpo);
    caixa.appendChild(rodape);
    fundo.appendChild(caixa);
    document.body.appendChild(fundo);

    var controle = {
        corpo: corpo,
        rodape: rodape,
        fechar: function () {
            document.removeEventListener("keydown", aoTeclar);
            fundo.remove();
            if (opcoes.aoFechar) opcoes.aoFechar();
        }
    };

    function aoTeclar(ev) {
        if (ev.key === "Escape") controle.fechar();
    }
    document.addEventListener("keydown", aoTeclar);

    topo.querySelector(".modal-fechar").onclick = controle.fechar;
    fundo.addEventListener("mousedown", function (ev) {
        if (ev.target === fundo) controle.fechar();
    });

    (opcoes.botoes || []).forEach(function (b) {
        var botao = document.createElement("button");
        botao.type = "button";
        botao.className = "btn " + (b.classe || "btn-secundario");
        botao.textContent = b.rotulo;
        botao.onclick = async function () {
            if (b.aoClicar) {
                botao.disabled = true;
                try { await b.aoClicar(controle, botao); } finally { botao.disabled = false; }
            }
            if (b.fechar) controle.fechar();
        };
        rodape.appendChild(botao);
    });

    var primeiro = corpo.querySelector("input, select, textarea");
    if (primeiro) setTimeout(function () { primeiro.focus(); }, 50);

    return controle;
}

/* Pergunta Sim/Nao. Retorna Promise<boolean>. */
function confirmar(opcoes) {
    if (typeof opcoes === "string") opcoes = { mensagem: opcoes };
    return new Promise(function (resolver) {
        var respondido = false;
        function responder(v) {
            if (respondido) return;
            respondido = true;
            resolver(v);
        }
        abrirModal({
            titulo: opcoes.titulo || "Confirmar",
            html: "<p>" + esc(opcoes.mensagem || "Tem certeza?") + "</p>",
            aoFechar: function () { responder(false); },
            botoes: [
                { rotulo: opcoes.textoCancelar || "Voltar", classe: "btn-secundario", fechar: true },
                {
                    rotulo: opcoes.textoConfirmar || "Confirmar",
                    classe: opcoes.perigo ? "btn-perigo" : "btn-primario",
                    aoClicar: function (m) { responder(true); m.fechar(); }
                }
            ]
        });
    });
}

/* Cria um seletor de 1 a 5 estrelas dentro de "container". Retorna { valor: () => n }. */
function seletorEstrelas(container, valorInicial, aoMudar) {
    var valor = valorInicial || 0;
    container.innerHTML = '<span class="seletor-estrelas"></span>';
    var area = container.firstChild;
    function desenhar() {
        area.innerHTML = "";
        for (var i = 1; i <= 5; i++) {
            (function (n) {
                var b = document.createElement("button");
                b.type = "button";
                b.textContent = "★";
                b.className = n <= valor ? "cheia" : "";
                b.setAttribute("aria-label", n + " estrela(s)");
                b.onclick = function () { valor = n; desenhar(); if (aoMudar) aoMudar(n); };
                area.appendChild(b);
            })(i);
        }
    }
    desenhar();
    return { valor: function () { return valor; } };
}

/* ---------- Navegacao entre "paginas" (secoes) ---------- */

/*
 HTML esperado:  <nav class="menu" id="menu"></nav>   e   <section class="pagina escondido" id="pagina-inicio">...</section>
 Paginas.menu([{ id: "inicio", rotulo: "Início" }, ...]);
 Paginas.aoAbrir("agenda", function () { ... });   // chamado SEMPRE que a pagina e aberta
 Paginas.ir("agenda");
*/
var Paginas = {
    _handlers: {},
    _atual: null,
    aoAbrir: function (id, funcao) { Paginas._handlers[id] = funcao; },
    atual: function () { return Paginas._atual; },
    ir: function (id) {
        var alvo = document.getElementById("pagina-" + id);
        if (!alvo) return;
        document.querySelectorAll(".pagina").forEach(function (p) { p.classList.add("escondido"); });
        alvo.classList.remove("escondido");
        document.querySelectorAll("#menu button").forEach(function (b) {
            b.classList.toggle("ativo", b.getAttribute("data-pagina") === id);
        });
        Paginas._atual = id;
        try { history.replaceState(null, "", "#" + id); } catch (e) { /* ignora */ }
        window.scrollTo(0, 0);
        if (Paginas._handlers[id]) {
            try {
                var r = Paginas._handlers[id]();
                if (r && r.catch) r.catch(function (e) { toast(e.message, "erro"); });
            } catch (e) { toast(e.message, "erro"); }
        }
    },
    menu: function (itens) {
        var nav = document.getElementById("menu");
        nav.innerHTML = "";
        itens.forEach(function (item) {
            var b = document.createElement("button");
            b.type = "button";
            b.setAttribute("data-pagina", item.id);
            b.textContent = item.rotulo;
            b.onclick = function () { Paginas.ir(item.id); };
            nav.appendChild(b);
        });
        var inicial = (location.hash || "").replace("#", "");
        var existe = itens.some(function (i) { return i.id === inicial; });
        Paginas.ir(existe ? inicial : itens[0].id);
    }
};

/* ---------- Cabecalho (logo, sino de notificacoes, usuario, sair) ---------- */

function montarCabecalho(usuario) {
    var topo = document.getElementById("cabecalho");
    var painelAberto = false;
    var ultimo = { naoLidas: 0, itens: [] };

    topo.innerHTML =
        '<div class="logo"><a href="' + paginaInicialDo(usuario.tipo) + '"><span class="marca"><img src="imagens/logo-branca.png" alt=""><b>Spa Delas</b></span></a></div>' +
        '<div class="usuario-topo">' +
        '  <button type="button" class="btn-sino" id="btnSino" aria-label="Notificações"><img class="sino-img" src="imagens/sino.jpg" alt="" onerror="this.replaceWith(document.createTextNode(\'🔔\'))"><span class="contador escondido" id="contadorSino">0</span></button>' +
        '  <div class="painel-notificacoes escondido" id="painelNotificacoes"></div>' +
        '  <button type="button" class="perfil-topo" id="btnPerfilTopo"></button>' +
        '  <button type="button" class="btn btn-sair" id="btnSair">Sair</button>' +
        "</div>";

    // Rodape com a logo, logo abaixo do conteudo da pagina.
    var principal = document.querySelector("main.conteudo");
    if (principal && !document.getElementById("rodape")) {
        var rodape = document.createElement("footer");
        rodape.id = "rodape"; rodape.className = "rodape";
        rodape.innerHTML = '<img src="imagens/logo.png" alt=""><span>Spa Delas · Realce a sua beleza.</span>';
        principal.insertAdjacentElement("afterend", rodape);
    }

    function desenharUsuario() {
        var u = (Sessao.obter() || { usuario: usuario }).usuario;
        document.getElementById("btnPerfilTopo").innerHTML =
            avatarHtml(u.fotoUrl, u.nome, 34) + "<span>Olá, <b>" + esc(u.nome.split(" ")[0]) + "</b></span>";
    }

    function desenharNotificacoes() {
        var contador = document.getElementById("contadorSino");
        contador.textContent = ultimo.naoLidas > 99 ? "99+" : ultimo.naoLidas;
        contador.classList.toggle("escondido", ultimo.naoLidas === 0);

        var painel = document.getElementById("painelNotificacoes");
        if (!painelAberto) return;

        var html = '<header><strong>Notificações</strong><button type="button" class="btn-link" id="btnLerTodas">Marcar todas como lidas</button></header><div class="lista">';
        if (ultimo.itens.length === 0) {
            html += '<div class="vazio" style="border:none">Nenhuma notificação por enquanto.</div>';
        }
        ultimo.itens.forEach(function (n) {
            html += '<div class="notificacao ' + (n.lida ? "" : "nao-lida") + '" data-id="' + n.id + '">' +
                "<strong>" + esc(n.titulo) + "</strong><p>" + esc(n.mensagem) + "</p><small>" + esc(dataHoraBR(n.criadoEm)) + "</small></div>";
        });
        html += "</div>";
        painel.innerHTML = html;

        var todas = document.getElementById("btnLerTodas");
        if (todas) todas.onclick = async function () {
            ultimo = await tentar(function () { return api("/api/notificacoes/lidas", { method: "PUT" }); }) || ultimo;
            desenharNotificacoes();
        };
        painel.querySelectorAll(".notificacao.nao-lida").forEach(function (el) {
            el.onclick = async function () {
                ultimo = await tentar(function () { return api("/api/notificacoes/" + el.getAttribute("data-id") + "/lida", { method: "PUT" }); }) || ultimo;
                desenharNotificacoes();
            };
        });
    }

    async function carregarNotificacoes() {
        try {
            ultimo = await api("/api/notificacoes");
            desenharNotificacoes();
        } catch (e) { /* silencioso */ }
    }

    document.getElementById("btnSino").onclick = function (ev) {
        ev.stopPropagation();
        painelAberto = !painelAberto;
        document.getElementById("painelNotificacoes").classList.toggle("escondido", !painelAberto);
        if (painelAberto) carregarNotificacoes();
    };
    document.addEventListener("click", function (ev) {
        var painel = document.getElementById("painelNotificacoes");
        if (painelAberto && painel && !painel.contains(ev.target)) {
            painelAberto = false;
            painel.classList.add("escondido");
        }
    });

    document.getElementById("btnSair").onclick = sair;
    document.getElementById("btnPerfilTopo").onclick = function () {
        if (document.getElementById("pagina-conta")) Paginas.ir("conta");
    };
    document.addEventListener("usuario-atualizado", desenharUsuario);

    desenharUsuario();
    carregarNotificacoes();
    setInterval(carregarNotificacoes, 45000);

    // As telas podem pedir para atualizar o sino logo apos uma acao.
    window.atualizarNotificacoes = carregarNotificacoes;
}

/* ---------- Pagina "Minha conta" (igual para os 3 perfis) ---------- */

/*
 Em cada tela:  <section class="pagina escondido" id="pagina-conta"><div id="areaConta"></div></section>
                Paginas.aoAbrir("conta", () => montarConta(document.getElementById("areaConta")));
*/
function montarConta(container) {
    var s = Sessao.obter();
    if (!s) return;
    var u = s.usuario;
    var tipos = { Cliente: "Cliente", Barbeiro: "Profissional", Administrador: "Administrador" };

    container.innerHTML =
        '<div class="titulo-pagina"><h2>Minha conta</h2><p>Atualize sua foto, seus dados e sua senha.</p></div>' +
        '<div class="grade-2" style="align-items:start">' +
        '  <div class="card"><h3>Foto de perfil</h3><p class="card-sub">JPG, PNG ou WEBP de até 3 MB.</p>' +
        '    <div class="flex" style="gap:20px">' +
        '      <div id="contaAvatar"></div>' +
        '      <div>' +
        '        <p class="negrito" id="contaNomeTexto"></p><p class="texto-suave texto-pequeno" id="contaEmailTexto"></p>' +
        '        <span class="badge badge-info">' + esc(tipos[u.tipo] || u.tipo) + "</span>" +
        "      </div>" +
        "    </div>" +
        '    <div class="flex mt">' +
        '      <input type="file" id="contaArquivo" accept="image/png,image/jpeg,image/webp" class="escondido">' +
        '      <button type="button" class="btn btn-primario" id="contaEnviarFoto">Enviar nova foto</button>' +
        '      <button type="button" class="btn btn-secundario" id="contaRemoverFoto">Remover foto</button>' +
        "    </div>" +
        "  </div>" +
        '  <div class="card"><h3>Dados da conta</h3><p class="card-sub">Seu nome e e-mail de acesso.</p>' +
        '    <div class="campo"><label for="contaNome">Nome</label><input id="contaNome" type="text" maxlength="150"></div>' +
        '    <div class="campo"><label for="contaEmail">E-mail</label><input id="contaEmail" type="email" maxlength="150"></div>' +
        '    <button type="button" class="btn btn-primario" id="contaSalvar">Salvar dados</button>' +
        "  </div>" +
        '  <div class="card"><h3>Alterar senha</h3><p class="card-sub">Use pelo menos 6 caracteres.</p>' +
        '    <div class="campo"><label for="senhaAtual">Senha atual</label><input id="senhaAtual" type="password" autocomplete="current-password"></div>' +
        '    <div class="campo"><label for="senhaNova">Nova senha</label><input id="senhaNova" type="password" autocomplete="new-password"></div>' +
        '    <div class="campo"><label for="senhaConfirma">Confirmar nova senha</label><input id="senhaConfirma" type="password" autocomplete="new-password"></div>' +
        '    <button type="button" class="btn btn-primario" id="contaSalvarSenha">Alterar senha</button>' +
        "  </div>" +
        "</div>";

    function desenhar() {
        var atual = Sessao.obter().usuario;
        document.getElementById("contaAvatar").innerHTML = avatarHtml(atual.fotoUrl, atual.nome, 96);
        document.getElementById("contaNomeTexto").textContent = atual.nome;
        document.getElementById("contaEmailTexto").textContent = atual.email;
        document.getElementById("contaNome").value = atual.nome;
        document.getElementById("contaEmail").value = atual.email;
        document.getElementById("contaRemoverFoto").classList.toggle("escondido", !atual.fotoUrl);
    }
    desenhar();

    document.getElementById("contaEnviarFoto").onclick = function () {
        document.getElementById("contaArquivo").click();
    };

    document.getElementById("contaArquivo").onchange = async function (ev) {
        var arquivo = ev.target.files[0];
        ev.target.value = "";
        if (!arquivo) return;
        if (arquivo.size > 3 * 1024 * 1024) { toast("A imagem deve ter no máximo 3 MB.", "erro"); return; }
        var form = new FormData();
        form.append("arquivo", arquivo);
        var atualizado = await tentar(function () { return api("/api/usuarios/me/foto", { method: "POST", body: form }); });
        if (atualizado) {
            Sessao.atualizarUsuario(atualizado);
            desenhar();
            toast("Foto atualizada!", "ok");
        }
    };

    document.getElementById("contaRemoverFoto").onclick = async function () {
        var atualizado = await tentar(function () { return api("/api/usuarios/me/foto", { method: "DELETE" }); });
        if (atualizado) {
            Sessao.atualizarUsuario(atualizado);
            desenhar();
            toast("Foto removida.", "ok");
        }
    };

    document.getElementById("contaSalvar").onclick = async function () {
        var corpo = {
            nome: document.getElementById("contaNome").value,
            email: document.getElementById("contaEmail").value
        };
        var atualizado = await tentar(function () { return api("/api/usuarios/me", { method: "PUT", body: corpo }); });
        if (atualizado) {
            Sessao.atualizarUsuario(atualizado);
            desenhar();
            toast("Dados atualizados!", "ok");
        }
    };

    document.getElementById("contaSalvarSenha").onclick = async function () {
        var atual = document.getElementById("senhaAtual").value;
        var nova = document.getElementById("senhaNova").value;
        var confirma = document.getElementById("senhaConfirma").value;
        if (nova !== confirma) { toast("A confirmação não confere com a nova senha.", "erro"); return; }
        var ok = await tentar(function () {
            return api("/api/usuarios/me/senha", { method: "PUT", body: { senhaAtual: atual, novaSenha: nova } });
        });
        if (ok) {
            document.getElementById("senhaAtual").value = "";
            document.getElementById("senhaNova").value = "";
            document.getElementById("senhaConfirma").value = "";
            toast("Senha alterada com sucesso!", "ok");
        }
    };
}

/* ---------- Imagens que falham ao carregar ---------- */
// Foto de perfil quebrada -> perfil.png; foto de portfolio quebrada -> generico.jpg.
document.addEventListener("error", function (ev) {
    var img = ev.target;
    if (!img || img.tagName !== "IMG" || img.dataset.fallback) return;
    var destino = null;
    if (img.closest(".av")) destino = "imagens/perfil.png";
    else if (img.closest(".galeria")) destino = "imagens/generico.png";
    if (destino) { img.dataset.fallback = "1"; img.src = destino; }
}, true);
