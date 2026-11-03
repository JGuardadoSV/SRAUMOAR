/* Selección territorial compartida. Nunca interpreta IDs o nombres del modelo legacy como códigos fiscales. */
(function () {
    'use strict';
    const selectores = new WeakMap();
    function iniciarSelector(root) {
        if (selectores.has(root)) return selectores.get(root);
        const datos = JSON.parse(root.querySelector('[data-territorio-datos]').textContent);
        const dep = root.querySelector('[data-territorio-departamento]');
        const mun = root.querySelector('[data-territorio-municipio]');
        const dis = root.querySelector('[data-territorio-distrito]');
        function opciones(select, entradas, mensaje, seleccionado) {
            select.replaceChildren(new Option(mensaje, ''));
            entradas.forEach(e => select.add(new Option(e.codigo + ' - ' + e.nombre, e.codigo)));
            select.value = entradas.some(e => e.codigo === seleccionado) ? seleccionado : '';
            select.disabled = entradas.length === 0;
        }
        function distrito(valor) {
            opciones(dis, datos.filter(e => e.catalogo === '008' && e.departamento === dep.value && e.municipio === mun.value),
                mun.value ? 'Seleccione distrito' : 'Seleccione municipio primero', valor);
        }
        function municipio(valor, valorDistrito) {
            opciones(mun, datos.filter(e => e.catalogo === '013' && e.departamento === dep.value),
                dep.value ? 'Seleccione municipio' : 'Seleccione departamento primero', valor);
            distrito(valorDistrito);
        }
        function establecer(valores) {
            opciones(dep, datos.filter(e => e.catalogo === '012'), 'Seleccione departamento', valores.departamento);
            municipio(valores.municipio, valores.distrito);
        }
        dep.addEventListener('change', () => municipio('', ''));
        mun.addEventListener('change', () => distrito(''));
        const api = { establecer, valores: () => ({ departamento: dep.value, municipio: mun.value, distrito: dis.value }) };
        selectores.set(root, api);
        establecer(root.dataset);
        return api;
    }
    async function iniciarAlumno(root) {
        const dialog = root.querySelector('dialog');
        const form = root.querySelector('[data-ubicacion-form]');
        const selector = iniciarSelector(root.querySelector('[data-territorio-fiscal]'));
        const resumen = root.querySelector('[data-ubicacion-resumen]');
        const error = root.querySelector('[data-ubicacion-error]');
        const guardar = root.querySelector('[data-ubicacion-guardar]');
        const cerrar = root.querySelector('[data-ubicacion-cerrar]');
        const cancelar = root.querySelector('[data-ubicacion-cancelar]');
        const url = '/api/ubicacion-fiscal/alumnos/' + encodeURIComponent(root.dataset.ubicacionAlumno);
        let perfil = null;
        let cargando = false;
        async function cargar(abrir) {
            if (cargando) return;
            cargando = true;
            guardar.disabled = true;
            error.textContent = '';
            try {
                const respuesta = await fetch(url, { credentials: 'same-origin', cache: 'no-store' });
                if (!respuesta.ok) throw new Error('No se pudo cargar la ubicación. Intente nuevamente.');
                perfil = await respuesta.json();
                selector.establecer(perfil);
                form.elements.complemento.value = perfil.complemento || '';
                resumen.textContent = perfil.valida ? perfil.descripcion : 'Complete Departamento → Municipio → Distrito antes de continuar.';
                cerrar.hidden = !perfil.valida;
                cancelar.hidden = perfil.valida;
                guardar.disabled = false;
                if (!dialog.open && (abrir || (!perfil.valida && root.dataset.abrirSiFalta === 'true'))) dialog.showModal();
            } catch (e) {
                perfil = null;
                resumen.textContent = e.message;
                error.textContent = e.message;
                if (!dialog.open && (abrir || root.dataset.abrirSiFalta === 'true')) dialog.showModal();
            } finally { cargando = false; }
        }
        root.querySelector('[data-ubicacion-abrir]').addEventListener('click', () => cargar(true));
        root.querySelector('[data-ubicacion-recargar]').addEventListener('click', () => cargar(true));
        cerrar.addEventListener('click', () => dialog.close());
        dialog.addEventListener('cancel', e => { if (!perfil || !perfil.valida) e.preventDefault(); });
        // Bloqueo de navegación/formularios hacia el cobro mientras falte la ubicación.
        document.addEventListener('submit', e => {
            if (e.target === form || root.dataset.abrirSiFalta !== 'true' || perfil?.valida) return;
            e.preventDefault();
            cargar(true);
        }, true);
        form.addEventListener('submit', async e => {
            e.preventDefault();
            if (cargando || !perfil || !form.reportValidity()) return;
            cargando = true;
            guardar.disabled = true;
            error.textContent = '';
            try {
                const respuesta = await fetch(url, {
                    method: 'POST', credentials: 'same-origin',
                    headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': form.querySelector('[name="__RequestVerificationToken"]').value },
                    body: JSON.stringify({ ...selector.valores(), complemento: form.elements.complemento.value, version: perfil.version })
                });
                const datos = await respuesta.json().catch(() => ({}));
                if (!respuesta.ok) throw new Error(datos.mensaje || 'No se guardó la ubicación. Revise los datos e intente nuevamente.');
                perfil = datos;
                resumen.textContent = datos.descripcion;
                cerrar.hidden = false;
                cancelar.hidden = true;
                dialog.close();
            } catch (e) { error.textContent = e.message; }
            finally { cargando = false; guardar.disabled = false; }
        });
        await cargar(false);
    }
    window.TerritorioFiscal = { iniciarSelector };
    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('[data-territorio-fiscal]').forEach(iniciarSelector);
        document.querySelectorAll('[data-ubicacion-alumno]').forEach(iniciarAlumno);
    });
})();
