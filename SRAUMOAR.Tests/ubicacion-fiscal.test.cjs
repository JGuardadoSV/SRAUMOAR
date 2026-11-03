const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.join(__dirname, '..', 'SRAUMOAR');
const script = fs.readFileSync(path.join(root, 'wwwroot/js/ubicacion-fiscal.js'), 'utf8');
const fuente = JSON.parse(fs.readFileSync(path.join(root, 'Data/Catalogos/territorio-fiscal.json'), 'utf8'));
const datos = fuente.entradas.map(e => ({ catalogo: e.catalogo, codigo: e.codigo, nombre: e.valor, departamento: e.ambito, municipio: e.municipioCodigo }));

class Elemento {
    constructor() { this.eventos = {}; this.dataset = {}; this.textContent = ''; this.hidden = false; this.disabled = false; }
    addEventListener(tipo, fn) { (this.eventos[tipo] ||= []).push(fn); }
    async emitir(tipo, evento = {}) { await Promise.all((this.eventos[tipo] || []).map(fn => fn(evento))); }
}
class Select extends Elemento {
    constructor() { super(); this.options = []; this.seleccion = ''; }
    replaceChildren(...items) { this.options = [...items]; this.seleccion = ''; }
    add(item) { this.options.push(item); }
    set value(value) { this.seleccion = this.options.some(o => o.value === value) ? value : ''; }
    get value() { return this.seleccion; }
}
function entorno({ alumno = false, perfil, errorCarga = false, inicial = {} } = {}) {
    const doc = new Elemento();
    const selector = new Elemento();
    selector.dataset = inicial;
    const dep = new Select(), mun = new Select(), dis = new Select();
    selector.querySelector = nombre => ({ '[data-territorio-datos]': { textContent: JSON.stringify(datos) },
        '[data-territorio-departamento]': dep, '[data-territorio-municipio]': mun, '[data-territorio-distrito]': dis })[nombre];
    const modal = new Elemento();
    modal.dataset = { ubicacionAlumno: '23', abrirSiFalta: 'true' };
    const dialog = new Elemento();
    dialog.open = false;
    dialog.showModal = () => { dialog.open = true; };
    dialog.close = () => { dialog.open = false; };
    const form = new Elemento();
    form.elements = { complemento: { value: '' } };
    form.reportValidity = () => !!dep.value && !!mun.value && !!dis.value && !!form.elements.complemento.value;
    form.querySelector = () => ({ value: 'antiforgery-sintetico' });
    const campos = { dialog, '[data-ubicacion-form]': form, '[data-territorio-fiscal]': selector };
    for (const nombre of ['resumen', 'error', 'guardar', 'cerrar', 'cancelar', 'abrir', 'recargar']) campos[`[data-ubicacion-${nombre}]`] = new Elemento();
    modal.querySelector = nombre => campos[nombre];
    doc.querySelectorAll = nombre => nombre === '[data-territorio-fiscal]' ? [selector] : alumno ? [modal] : [];
    let actual = perfil || { departamento: null, municipio: null, distrito: null, complemento: 'Residencia', valida: false, version: 'v1' };
    const solicitudes = [];
    const context = { window: {}, document: doc, console, Option: class { constructor(text, value) { this.text = text; this.value = value; } },
        fetch: async (url, options) => {
            solicitudes.push({ url, options });
            assert.equal(url, '/api/ubicacion-fiscal/alumnos/23');
            if (!options.method) return { ok: !errorCarga, json: async () => actual };
            const body = JSON.parse(options.body);
            actual = { ...body, valida: true, descripcion: 'Santa Tecla (11), La Libertad Sur (28), La Libertad (05)', version: 'v2' };
            return { ok: true, json: async () => actual };
        } };
    vm.runInNewContext(script, context);
    return { context, doc, selector, dep, mun, dis, dialog, form, campos, solicitudes };
}
const esperar = () => new Promise(setImmediate);

test('cambiar departamento limpia municipio y distrito; cambiar municipio limpia distrito', async () => {
    const e = entorno({ inicial: { departamento: '05', municipio: '28', distrito: '11' } });
    const api = e.context.window.TerritorioFiscal.iniciarSelector(e.selector);
    assert.deepEqual(JSON.parse(JSON.stringify(api.valores())), { departamento: '05', municipio: '28', distrito: '11' });
    e.dep.value = '09';
    await e.dep.emitir('change');
    assert.equal(e.mun.value, '');
    assert.equal(e.dis.value, '');
    assert.equal(e.dis.disabled, true);
    assert.equal(e.mun.options.some(x => x.value === '28'), false);
    e.mun.value = '11';
    await e.mun.emitir('change');
    assert.equal(e.dis.disabled, false);
    assert.equal(e.dis.value, '');
    assert.equal(e.dis.options.some(x => x.value === '03' && x.text.includes('ILOBASCO')), true);
    api.establecer({ departamento: '05', municipio: '26', distrito: '11' });
    assert.equal(e.dis.value, ''); // Santa Tecla no pertenece a La Libertad Este.
});

test('cada distrito conserva su padre al cargar un perfil guardado', () => {
    const e = entorno();
    const api = e.context.window.TerritorioFiscal.iniciarSelector(e.selector);
    for (const distrito of fuente.entradas.filter(x => x.catalogo === '008')) {
        const perfil = { departamento: distrito.ambito, municipio: distrito.municipioCodigo, distrito: distrito.codigo };
        api.establecer(perfil);
        assert.deepEqual(JSON.parse(JSON.stringify(api.valores())), perfil);
    }
});

test('alumno sin ubicación abre modal, bloquea continuar y guarda con antiforgery antes de habilitar', async () => {
    const e = entorno({ alumno: true });
    await e.doc.emitir('DOMContentLoaded');
    await esperar();
    assert.equal(e.dialog.open, true);
    let bloqueado = false;
    await e.doc.emitir('submit', { target: {}, preventDefault: () => { bloqueado = true; } });
    await esperar();
    assert.equal(bloqueado, true);
    e.context.window.TerritorioFiscal.iniciarSelector(e.selector).establecer({ departamento: '05', municipio: '28', distrito: '11' });
    e.form.elements.complemento.value = 'Dirección corregida';
    await e.form.emitir('submit', { preventDefault() {} });
    const envio = e.solicitudes.find(x => x.options.method === 'POST');
    assert.equal(envio.options.headers['X-XSRF-TOKEN'], 'antiforgery-sintetico');
    assert.deepEqual(JSON.parse(envio.options.body), { departamento: '05', municipio: '28', distrito: '11', complemento: 'Dirección corregida', version: 'v1' });
    assert.equal(e.dialog.open, false);
    let bloqueadoTrasGuardar = false;
    await e.doc.emitir('submit', { target: {}, preventDefault: () => { bloqueadoTrasGuardar = true; } });
    assert.equal(bloqueadoTrasGuardar, false);
    assert.match(e.campos['[data-ubicacion-resumen]'].textContent, /Santa Tecla/);
});

test('si falla la carga el modal no permite guardar ni continuar', async () => {
    const e = entorno({ alumno: true, errorCarga: true });
    await e.doc.emitir('DOMContentLoaded');
    await esperar();
    assert.equal(e.dialog.open, true);
    assert.equal(e.campos['[data-ubicacion-guardar]'].disabled, true);
    let cancelar = false;
    await e.dialog.emitir('cancel', { preventDefault: () => { cancelar = true; } });
    assert.equal(cancelar, true);
    await e.form.emitir('submit', { preventDefault() {} });
    assert.equal(e.solicitudes.some(x => x.options.method === 'POST'), false);
});
