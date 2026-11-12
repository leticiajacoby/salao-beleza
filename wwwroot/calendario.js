/* =====================================================================
   calendario.js - agenda em formato de calendario (dia / semana).
   Depende de comum.js. Uso:

     var cal = Calendario.montar(document.getElementById("calendario"), {
         modo: "semana",                 // "dia" ou "semana"
         data: new Date(),               // dia de referencia
         horaInicio: 7, horaFim: 21,     // faixa de horas exibida
         mostrarBarbeiro: false,         // true para o admin (mostra o nome da profissional no bloco)
         buscar: async function (de, ate) {          // de/ate = "2026-10-12T00:00:00" (ate e exclusivo)
             return await api("/api/agendamentos?de=" + de + "&ate=" + ate);
         },
         aoClicar: function (agendamento) { ... }
     });
     cal.recarregar();  cal.irPara(new Date());  cal.setModo("dia");
   ===================================================================== */

var Calendario = (function () {
    var PX_POR_HORA = 60; // 1 pixel por minuto

    function inicioDaSemana(d) {
        var r = new Date(d.getFullYear(), d.getMonth(), d.getDate());
        r.setDate(r.getDate() - r.getDay()); // domingo
        return r;
    }

    function minutosDoDia(d) {
        return d.getHours() * 60 + d.getMinutes();
    }

    function distribuir(eventos) {
        eventos.sort(function (a, b) { return a.ini - b.ini || a.fim - b.fim; });

        var cluster = null;
        var fimCluster = 0;
        var clusters = [];

        eventos.forEach(function (e) {
            if (!cluster || e.ini >= fimCluster) {
                cluster = [];
                clusters.push(cluster);
                fimCluster = 0;
            }
            cluster.push(e);
            fimCluster = Math.max(fimCluster, e.fim);
        });

        clusters.forEach(function (grupo) {
            var faixas = [];
            grupo.forEach(function (e) {
                var i = 0;
                while (faixas[i] !== undefined && faixas[i] > e.ini) i++;
                faixas[i] = e.fim;
                e.faixa = i;
            });
            grupo.forEach(function (e) { e.faixas = faixas.length; });
        });
    }

    function montar(container, opcoes) {
        var estado = {
            modo: opcoes.modo || "semana",
            data: opcoes.data ? new Date(opcoes.data) : new Date(),
            horaInicio: opcoes.horaInicio !== undefined ? opcoes.horaInicio : 7,
            horaFim: opcoes.horaFim !== undefined ? opcoes.horaFim : 21,
            mostrarCancelados: false,
            agendamentos: [],
            carregando: false
        };

        function diasVisiveis() {
            if (estado.modo === "dia") {
                return [new Date(estado.data.getFullYear(), estado.data.getMonth(), estado.data.getDate())];
            }
            var inicio = inicioDaSemana(estado.data);
            var dias = [];
            for (var i = 0; i < 7; i++) dias.push(somarDias(inicio, i));
            return dias;
        }

        function titulo(dias) {
            if (dias.length === 1) {
                return dias[0].toLocaleDateString("pt-BR", { weekday: "long", day: "numeric", month: "long", year: "numeric" });
            }
            var a = dias[0];
            var b = dias[dias.length - 1];
            var mesA = a.toLocaleDateString("pt-BR", { month: "short" }).replace(".", "");
            var mesB = b.toLocaleDateString("pt-BR", { month: "short" }).replace(".", "");
            return a.getDate() + " " + mesA + " – " + b.getDate() + " " + mesB + " " + b.getFullYear();
        }

        async function recarregar() {
            var dias = diasVisiveis();
            var de = isoData(dias[0]) + "T00:00:00";
            var ate = isoData(somarDias(dias[dias.length - 1], 1)) + "T00:00:00";

            estado.carregando = true;
            desenhar();

            try {
                estado.agendamentos = (await opcoes.buscar(de, ate)) || [];
            } catch (e) {
                estado.agendamentos = [];
                toast(e.message, "erro");
            }

            estado.carregando = false;
            desenhar();
        }

        function desenhar() {
            var dias = diasVisiveis();
            var horas = estado.horaFim - estado.horaInicio;
            var altura = horas * PX_POR_HORA;
            var hojeIso = isoData(new Date());

            var html = '<div class="cal-barra">' +
                '<div class="cal-nav">' +
                '<button type="button" class="btn btn-secundario btn-pequeno" data-cal="ant" aria-label="Anterior">‹</button>' +
                '<button type="button" class="btn btn-secundario btn-pequeno" data-cal="hoje">Hoje</button>' +
                '<button type="button" class="btn btn-secundario btn-pequeno" data-cal="prox" aria-label="Próximo">›</button>' +
                "</div>" +
                "<h3>" + esc(titulo(dias)) + "</h3>" +
                '<div class="cal-nav">' +
                '<label class="check texto-pequeno"><input type="checkbox" data-cal="cancelados"' + (estado.mostrarCancelados ? " checked" : "") + "> Mostrar cancelados</label>" +
                '<button type="button" class="btn btn-pequeno ' + (estado.modo === "dia" ? "btn-primario" : "btn-secundario") + '" data-cal="dia">Dia</button>' +
                '<button type="button" class="btn btn-pequeno ' + (estado.modo === "semana" ? "btn-primario" : "btn-secundario") + '" data-cal="semana">Semana</button>' +
                "</div></div>";

            html += '<div class="cal-moldura"><div class="cal" style="grid-template-columns:56px repeat(' + dias.length + ',minmax(0,1fr))">';

            // Cabecalho
            html += '<div class="cal-cab" style="background:var(--rosa-escuro)"></div>';
            dias.forEach(function (d) {
                var ehHoje = isoData(d) === hojeIso;
                html += '<div class="cal-cab ' + (ehHoje ? "hoje" : "") + '">' + esc(diaSemanaBR(d)) + "<strong>" + d.getDate() + "</strong></div>";
            });

            // Coluna de horas
            html += '<div class="cal-horas" style="height:' + altura + 'px">';
            for (var h = estado.horaInicio; h <= estado.horaFim; h++) {
                html += '<div class="cal-hora" style="top:' + (h - estado.horaInicio) * PX_POR_HORA + 'px">' + dois(h) + ":00</div>";
            }
            html += "</div>";

            // Colunas dos dias
            dias.forEach(function (d) {
                var chave = isoData(d);
                var ehHoje = chave === hojeIso;
                html += '<div class="cal-dia ' + (ehHoje ? "hoje" : "") + '" data-dia="' + chave + '" style="height:' + altura + 'px;background-size:100% ' + PX_POR_HORA + 'px"></div>';
            });

            html += "</div></div>";

            html += '<div class="cal-legenda">' +
                '<span><i style="background:var(--aviso-claro);border-left:3px solid var(--rosa)"></i>Agendado</span>' +
                '<span><i style="background:var(--ok-claro);border-left:3px solid var(--ok)"></i>Concluído</span>' +
                '<span><i style="background:var(--erro-claro);border-left:3px solid var(--erro)"></i>Faltou</span>' +
                '<span><i style="background:#efefef;border-left:3px solid #999"></i>Cancelado</span>' +
                (estado.carregando ? "<span>Carregando…</span>" : "") +
                "</div>";

            container.innerHTML = html;

            // Eventos de cada dia
            dias.forEach(function (d) {
                var chave = isoData(d);
                var coluna = container.querySelector('.cal-dia[data-dia="' + chave + '"]');

                var doDia = estado.agendamentos
                    .filter(function (a) {
                        if (!estado.mostrarCancelados && a.status === "Cancelado") return false;
                        return isoData(new Date(a.dataHora)) === chave;
                    })
                    .map(function (a) {
                        var inicio = new Date(a.dataHora);
                        var ini = minutosDoDia(inicio);
                        return { ag: a, ini: ini, fim: ini + Math.max(Number(a.duracaoMinutos) || 30, 20) };
                    });

                distribuir(doDia);

                doDia.forEach(function (e) {
                    var a = e.ag;
                    var topo = (e.ini - estado.horaInicio * 60) * (PX_POR_HORA / 60);
                    var alt = (e.fim - e.ini) * (PX_POR_HORA / 60);
                    if (topo + alt < 0 || topo > altura) return;

                    var largura = 100 / e.faixas;
                    var botao = document.createElement("button");
                    botao.type = "button";
                    botao.className = "cal-evento st-" + a.status;
                    botao.style.top = Math.max(topo, 0) + "px";
                    botao.style.height = Math.max(alt - 2, 18) + "px";
                    botao.style.left = "calc(" + e.faixa * largura + "% + 2px)";
                    botao.style.width = "calc(" + largura + "% - 4px)";
                    botao.title = horaBR(a.dataHora) + " – " + horaBR(a.fim) + "\n" + a.nomeCliente + "\n" + a.nomeServico + "\nProfissional: " + a.nomeBarbeiro + "\n" + a.status;
                    botao.innerHTML = "<strong>" + esc(horaBR(a.dataHora)) + " " + esc(a.nomeCliente) + "</strong>" +
                        esc(a.nomeServico) + (opcoes.mostrarBarbeiro ? "<br>✿ " + esc(a.nomeBarbeiro) : "");
                    botao.onclick = function () { if (opcoes.aoClicar) opcoes.aoClicar(a); };
                    coluna.appendChild(botao);
                });

                // Linha do horario atual
                if (chave === hojeIso) {
                    var agora = minutosDoDia(new Date());
                    var y = (agora - estado.horaInicio * 60) * (PX_POR_HORA / 60);
                    if (y >= 0 && y <= altura) {
                        var linha = document.createElement("div");
                        linha.className = "cal-agora";
                        linha.style.top = y + "px";
                        coluna.appendChild(linha);
                    }
                }
            });

            // Botoes da barra
            container.querySelectorAll("[data-cal]").forEach(function (el) {
                var acao = el.getAttribute("data-cal");
                if (acao === "cancelados") {
                    el.onchange = function () { estado.mostrarCancelados = el.checked; desenhar(); };
                    return;
                }
                el.onclick = function () {
                    var passo = estado.modo === "dia" ? 1 : 7;
                    if (acao === "ant") { estado.data = somarDias(estado.data, -passo); recarregar(); }
                    else if (acao === "prox") { estado.data = somarDias(estado.data, passo); recarregar(); }
                    else if (acao === "hoje") { estado.data = new Date(); recarregar(); }
                    else if (acao === "dia" || acao === "semana") { estado.modo = acao; recarregar(); }
                };
            });
        }

        recarregar();

        return {
            recarregar: recarregar,
            irPara: function (data) { estado.data = new Date(data); return recarregar(); },
            setModo: function (modo) { estado.modo = modo; return recarregar(); }
        };
    }

    return { montar: montar };
})();
