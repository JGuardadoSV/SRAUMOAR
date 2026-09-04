// Mejora progresiva de catálogos fiscales: el select conserva valor, binding y eventos.
document.addEventListener('DOMContentLoaded', () => {
    const selects = [...document.querySelectorAll('select[data-catalogo-modal]')];
    if (!selects.length || typeof bootstrap === 'undefined') return;
    const host = document.createElement('div');
    host.className = 'modal fade'; host.tabIndex = -1; host.setAttribute('aria-label', 'Buscar en catálogo fiscal');
    host.innerHTML = '<div class="modal-dialog modal-dialog-scrollable"><div class="modal-content"><div class="modal-header"><h2 class="modal-title fs-5">Buscar en catálogo fiscal</h2><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Cerrar"></button></div><div class="modal-body"><input type="search" class="form-control mb-3" aria-label="Buscar por código o nombre" placeholder="Código o nombre"><div class="list-group"></div></div></div></div>';
    document.body.append(host);
    const modal = new bootstrap.Modal(host), query = host.querySelector('input'), list = host.querySelector('.list-group');
    let active, trigger;
    function render() {
        list.replaceChildren();
        const term = query.value.trim().toLocaleLowerCase('es');
        const departamento = active.dataset.departamentoSelect ? document.getElementById(active.dataset.departamentoSelect)?.value : null;
        const municipio = active.dataset.municipioSelect ? document.getElementById(active.dataset.municipioSelect)?.value : null;
        [...active.options].filter(o => o.value && !o.disabled
            && (!active.dataset.departamentoSelect || o.dataset.departamento === departamento)
            && (!active.dataset.municipioSelect || o.dataset.municipio === municipio)
            && o.text.toLocaleLowerCase('es').includes(term)).slice(0, 100).forEach(o => {
            const b = document.createElement('button'); b.type = 'button'; b.className = 'list-group-item list-group-item-action'; b.textContent = o.text;
            b.addEventListener('click', () => {
                // El código puede repetirse en otro ámbito; seleccionar la opción exacta pulsada.
                active.selectedIndex = o.index;
                // jQuery conserva también los manejadores de cascadas de las páginas legacy.
                if (window.jQuery) window.jQuery(active).trigger('change');
                else active.dispatchEvent(new Event('change', { bubbles: true }));
                selects.forEach(s => s._actualizarCatalogo()); modal.hide();
            }); list.append(b);
        });
        if (!list.childElementCount) list.textContent = 'Sin resultados. Verifique los catálogos fiscales importados.';
    }
    function open(select, button) {
        const faltaDepartamento = select.dataset.departamentoSelect && !document.getElementById(select.dataset.departamentoSelect)?.value;
        const faltaMunicipio = select.dataset.municipioSelect && !document.getElementById(select.dataset.municipioSelect)?.value;
        if (faltaDepartamento || faltaMunicipio) {
            window.alert(faltaDepartamento ? 'Seleccione primero el departamento.' : 'Seleccione primero el municipio.');
            return;
        }
        active = select; trigger = button; query.value = ''; render(); modal.show();
    }
    query.addEventListener('input', render);
    host.addEventListener('shown.bs.modal', () => query.focus());
    host.addEventListener('hidden.bs.modal', () => trigger?.focus());
    selects.forEach(select => {
        const wrapper = document.createElement('div'); wrapper.className = 'input-group';
        const text = document.createElement('input'); text.className = 'form-control'; text.readOnly = true;
        text.setAttribute('aria-label', select.dataset.catalogoModal);
        const button = document.createElement('button'); button.type = 'button'; button.className = 'btn btn-outline-secondary'; button.textContent = 'Buscar';
        button.setAttribute('aria-label', 'Buscar ' + select.dataset.catalogoModal);
        wrapper.append(text, button); select.after(wrapper);
        const required = select.required; select.required = false; select.hidden = true;
        select._actualizarCatalogo = () => { text.value = select.value ? select.selectedOptions[0]?.text ?? '' : ''; text.placeholder = 'Seleccione en Buscar'; };
        select._actualizarCatalogo(); select.addEventListener('change', select._actualizarCatalogo);
        if (window.jQuery) window.jQuery(select).on('change', select._actualizarCatalogo);
        button.addEventListener('click', () => open(select, button));
        select.form?.addEventListener('submit', e => {
            if (required && !select.value && !e.defaultPrevented) { e.preventDefault(); open(select, button); }
        });
    });
});
