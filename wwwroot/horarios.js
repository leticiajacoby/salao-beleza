/* =====================================================================
   horarios.js - editor de expediente semanal e folgas de UMA profissional.
   Uso: EditorHorarios.montar(document.getElementById("area"), barbeiroId);
   Depende de comum.js. Usado nas telas do barbeiro e do administrador.
   ===================================================================== */
var EditorHorarios = (function () {
    var DIAS = ["Domingo", "Segunda-feira", "Terça-feira", "Quarta-feira", "Quinta-feira", "Sexta-feira", "Sábado"];

    async function montar(container, barbeiroId) {
        container.innerHTML = '<div class="carregando">Carregando…</div>';

        var horarios, folgas;
        try {
            horarios = await api("/api/barbeiros/" + barbeiroId + "/horarios");
            folgas = await api("/api/barbeiros/" + barbeiroId + "/folgas");
        } catch (e) {
            container.innerHTML = '<div class="vazio">' + esc(e.message) + "</div>";
            return;
        }

        function desenhar() {
            var linhas = "";
            for (var d = 0; d <= 6; d++) {
                var h = horarios.find(function (x) { return x.diaSemana === d; }) ||
                    { diaSemana: d, trabalha: false, inicio: "09:00", fim: "18:00", almocoInicio: null, almocoFim: null };
                linhas +=
                    '<tr data-dia="' + d + '">' +
                    '<td><label class="check"><input type="checkbox" class="h-trabalha"' + (h.trabalha ? " checked" : "") + "> <b>" + DIAS[d] + "</b></label></td>" +
                    '<td><input type="time" class="h-inicio" value="' + esc(h.inicio) + '"></td>' +
                    '<td><input type="time" class="h-fim" value="' + esc(h.fim) + '"></td>' +
                    '<td><input type="time" class="h-ai" value="' + esc(h.almocoInicio || "") + '"></td>' +
                    '<td><input type="time" class="h-af" value="' + esc(h.almocoFim || "") + '"></td>' +
                    "</tr>";
            }

            var listaFolgas = folgas.length
                ? folgas.map(function (f) {
                    var mesmoDia = isoData(f.dataInicio) === isoData(f.dataFim);
                    return '<div class="flex flex-entre" style="padding:10px 0;border-bottom:1px solid #eee">' +
                        "<div><b>" + (mesmoDia ? dataBR(f.dataInicio) : dataBR(f.dataInicio) + " até " + dataBR(f.dataFim)) + "</b>" +
                        (f.motivo ? ' <span class="texto-suave">— ' + esc(f.motivo) + "</span>" : "") + "</div>" +
                        '<button type="button" class="btn btn-perigo btn-pequeno" data-remover="' + f.id + '">Remover</button></div>';
                }).join("")
                : '<div class="vazio">Nenhuma folga ou bloqueio futuro.</div>';

            container.innerHTML =
                '<div class="card"><h3>Expediente semanal</h3>' +
                '<p class="card-sub">Marque os dias de trabalho e o horário. Deixe o almoço em branco se não houver pausa. Os horários livres para os clientes são calculados a partir daqui.</p>' +
                '<div class="tabela-container"><table style="min-width:560px"><thead><tr><th>Dia</th><th>Início</th><th>Fim</th><th>Almoço de</th><th>até</th></tr></thead><tbody>' + linhas + "</tbody></table></div>" +
                '<div class="mt"><button type="button" class="btn btn-primario" id="h-salvar">Salvar horários</button></div></div>' +
                '<div class="card"><h3>Folgas e bloqueio de datas</h3>' +
                '<p class="card-sub">Férias, atestado ou folga: a profissional não recebe agendamentos nesses dias.</p>' +
                '<div class="form-grade"><div class="campo"><label>Data inicial</label><input type="date" id="f-ini"></div>' +
                '<div class="campo"><label>Data final</label><input type="date" id="f-fim"></div>' +
                '<div class="campo largo"><label>Motivo (opcional)</label><input type="text" id="f-motivo" maxlength="150"></div></div>' +
                '<div class="mt"><button type="button" class="btn btn-rosa" id="f-add">Bloquear datas</button></div>' +
                '<div class="mt">' + listaFolgas + "</div></div>";

            var hoje = isoData(new Date());
            container.querySelector("#f-ini").min = hoje;
            container.querySelector("#f-fim").min = hoje;

            container.querySelector("#h-salvar").onclick = salvarHorarios;
            container.querySelector("#f-add").onclick = adicionarFolga;
            container.querySelectorAll("[data-remover]").forEach(function (b) {
                b.onclick = function () { removerFolga(b.getAttribute("data-remover")); };
            });
        }

        async function salvarHorarios() {
            var dias = [];
            var erro = "";
            container.querySelectorAll("tr[data-dia]").forEach(function (tr) {
                var d = Number(tr.getAttribute("data-dia"));
                var trabalha = tr.querySelector(".h-trabalha").checked;
                var ini = tr.querySelector(".h-inicio").value;
                var fim = tr.querySelector(".h-fim").value;
                var ai = tr.querySelector(".h-ai").value;
                var af = tr.querySelector(".h-af").value;
                if (trabalha && !erro) {
                    if (!ini || !fim || ini >= fim) erro = DIAS[d] + ": o início deve ser antes do fim.";
                    else if ((ai && !af) || (!ai && af)) erro = DIAS[d] + ": informe o início e o fim do almoço.";
                    else if (ai && (ai >= af || ai < ini || af > fim)) erro = DIAS[d] + ": o almoço deve ficar dentro do expediente.";
                }
                dias.push({ diaSemana: d, trabalha: trabalha, inicio: ini || "09:00", fim: fim || "18:00", almocoInicio: ai || null, almocoFim: af || null });
            });
            if (erro) { toast(erro, "erro"); return; }

            var botao = container.querySelector("#h-salvar");
            botao.disabled = true;
            try {
                horarios = await api("/api/barbeiros/" + barbeiroId + "/horarios", { method: "PUT", body: dias });
                toast("Horários salvos!", "ok");
                desenhar();
            } catch (e) { toast(e.message, "erro"); botao.disabled = false; }
        }

        async function adicionarFolga() {
            var ini = container.querySelector("#f-ini").value;
            var fim = container.querySelector("#f-fim").value || ini;
            if (!ini) { toast("Escolha a data inicial.", "erro"); return; }
            if (fim < ini) { toast("A data final não pode ser antes da inicial.", "erro"); return; }
            try {
                var r = await api("/api/barbeiros/" + barbeiroId + "/folgas", {
                    method: "POST",
                    body: { dataInicio: ini, dataFim: fim, motivo: container.querySelector("#f-motivo").value }
                });
                if (r.agendamentosAfetados > 0) {
                    toast("Datas bloqueadas. Atenção: há " + r.agendamentosAfetados + " agendamento(s) já marcado(s) nesse período; eles NÃO foram cancelados automaticamente.", "aviso");
                } else {
                    toast("Datas bloqueadas!", "ok");
                }
                folgas = await api("/api/barbeiros/" + barbeiroId + "/folgas");
                desenhar();
            } catch (e) { toast(e.message, "erro"); }
        }

        async function removerFolga(id) {
            if (!(await confirmar({ titulo: "Remover bloqueio", mensagem: "Liberar essas datas para agendamento?", textoConfirmar: "Remover", perigo: true }))) return;
            try {
                await api("/api/barbeiros/" + barbeiroId + "/folgas/" + id, { method: "DELETE" });
                folgas = await api("/api/barbeiros/" + barbeiroId + "/folgas");
                toast("Bloqueio removido.", "ok");
                desenhar();
            } catch (e) { toast(e.message, "erro"); }
        }

        desenhar();
    }

    return { montar: montar };
})();
