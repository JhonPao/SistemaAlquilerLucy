// LucyVest Control — interactividad mínima de las vistas
(function () {
    'use strict';

    document.querySelectorAll('[data-toggle-password]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var input = document.getElementById(btn.getAttribute('data-toggle-password'));
            if (!input) return;

            var mostrar = input.type === 'password';
            input.type = mostrar ? 'text' : 'password';

            var icon = btn.querySelector('.icon');
            if (icon) {
                icon.classList.toggle('icon-eye', mostrar);
                icon.classList.toggle('icon-eye-off', !mostrar);
            }

            btn.setAttribute('aria-label', mostrar ? 'Ocultar contraseña' : 'Mostrar contraseña');
        });
    });

    var sidebar = document.getElementById('sidebar-principal');
    var menuToggle = document.getElementById('mobile-menu-toggle');
    var menuBackdrop = document.getElementById('sidebar-backdrop');

    if (sidebar && menuToggle && menuBackdrop) {
        var cerrarMenu = function () {
            sidebar.classList.remove('is-open');
            menuBackdrop.classList.remove('is-visible');
            document.body.classList.remove('menu-open');
            menuToggle.setAttribute('aria-expanded', 'false');
            menuToggle.setAttribute('aria-label', 'Abrir menú');
        };

        var abrirMenu = function () {
            sidebar.classList.add('is-open');
            menuBackdrop.classList.add('is-visible');
            document.body.classList.add('menu-open');
            menuToggle.setAttribute('aria-expanded', 'true');
            menuToggle.setAttribute('aria-label', 'Cerrar menú');
        };

        menuToggle.addEventListener('click', function () {
            if (sidebar.classList.contains('is-open')) {
                cerrarMenu();
            } else {
                abrirMenu();
            }
        });

        menuBackdrop.addEventListener('click', cerrarMenu);

        sidebar.querySelectorAll('a').forEach(function (enlace) {
            enlace.addEventListener('click', cerrarMenu);
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape') {
                cerrarMenu();
            }
        });

        window.addEventListener('resize', function () {
            if (window.innerWidth > 980) {
                cerrarMenu();
            }
        });
    }
})();
