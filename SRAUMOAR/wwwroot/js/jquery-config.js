// Configuración centralizada de jQuery para evitar conflictos
(function() {
    'use strict';
    
    // Verificar si jQuery ya está cargado
    if (typeof jQuery === 'undefined') {
        console.warn('jQuery no está disponible');
        return;
    }
    
    // Verificar si jQuery UI está disponible
    if (typeof jQuery.ui === 'undefined') {
        console.warn('jQuery UI no está disponible');
        return;
    }
    
    // Configuración global de jQuery
    jQuery.noConflict();
    
    // Función para inicializar autocomplete de forma segura
    window.initializeAutocomplete = function(selector, options) {
        try {
            var $element = jQuery(selector);
            if ($element.length === 0) {
                console.warn('Elemento no encontrado:', selector);
                return;
            }
            
            var autocomplete = $element.autocomplete(options);
            
            // Configurar _renderItem de forma segura
            setTimeout(function() {
                try {
                    var instance = $element.autocomplete("instance");
                    if (instance && typeof instance._renderItem === 'function') {
                        instance._renderItem = function(ul, item) {
                            return jQuery("<li>")
                                .append(`<button type='button' class='student-item'>
                                    <img src='${item.photoUrl || '/images/default.PNG'}'
                                         class='student-photo'
                                         alt='Foto de ${item.name}'
                                         onerror="this.src='/images/default.PNG'">
                                    <div class='student-info'>
                                        <div class='student-name'>${item.name}</div>
                                    </div>
                                </button>`)
                                .appendTo(ul);
                        };
                    }
                } catch (error) {
                    console.log("Error configurando _renderItem:", error);
                }
            }, 100);
            
            return autocomplete;
        } catch (error) {
            console.error('Error inicializando autocomplete:', error);
        }
    };
    
    // Función para mostrar detalles del estudiante
    window.showStudentDetails = function(student) {
        try {
            jQuery("#searchResults").html(`
                <div class="student-details-card">
                    <button type="button" class="close-btn" onclick="jQuery('#searchResults').empty()">
                        <i class="bi bi-x-lg"></i>
                    </button>
                    <div class="d-flex flex-column align-items-center">
                        <img src="${student.photoUrl || '/images/default.PNG'}"
                             class="student-photo-lg mb-2"
                             alt="Foto de ${student.name}"
                             onerror="this.src='/images/default.PNG'">
                        <div class="student-name-lg">${student.name}</div>
                    </div>
                    <div class="actions d-flex justify-content-center">
                        <a href="/aranceles/Cobrar?id=${student.id}" class="btn btn-primary btn-action"><i class="bi bi-cash-coin"></i> Aranceles</a>
                        <a href="/inscripcion/Create?id=${student.id}" class="btn btn-success btn-action"><i class="bi bi-arrow-right-circle"></i> Inscribir ciclo</a>
                        <a href="/inscripcion/MateriasInscritas?id=${student.id}" class="btn btn-outline-secondary btn-action"><i class="bi bi-journal-plus"></i> Inscribir materias</a>
                    </div>
                </div>
            `);
        } catch (error) {
            console.error('Error mostrando detalles del estudiante:', error);
        }
    };
    
    // Función para abrir PDF en popup
    window.abrirPdfEnPopup = function(url, titulo = 'Reporte') {
        try {
            const ancho = 900;
            const alto = 850;
            const left = (screen.width - ancho) / 2;
            const top = (screen.height - alto) / 2;

            const opciones = `
                width=${ancho},
                height=${alto},
                left=${left},
                top=${top},
                scrollbars=yes,
                resizable=yes,
                toolbar=no,
                menubar=no,
                location=no,
                directories=no,
                status=no
            `;

            window.open(url, titulo, opciones);
        } catch (error) {
            console.error('Error abriendo PDF:', error);
        }
    };
    
    console.log('jQuery configurado correctamente');
    
})();
