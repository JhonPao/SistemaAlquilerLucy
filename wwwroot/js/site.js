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
})();
