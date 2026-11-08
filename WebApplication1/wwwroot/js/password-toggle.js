// Ajoute un bouton « œil » à chaque champ mot de passe pour afficher / masquer ce qui est tapé.
(() => {
    function addToggle(input) {
        if (input.dataset.pwdToggle) return;
        input.dataset.pwdToggle = '1';

        const btn = document.createElement('button');
        btn.type = 'button';
        btn.tabIndex = -1;
        btn.setAttribute('aria-label', 'Afficher le mot de passe');
        btn.title = 'Afficher le mot de passe';
        btn.innerHTML = '<i class="bi bi-eye"></i>';

        btn.addEventListener('click', () => {
            const show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            btn.innerHTML = `<i class="bi ${show ? 'bi-eye-slash' : 'bi-eye'}"></i>`;
            const label = show ? 'Masquer le mot de passe' : 'Afficher le mot de passe';
            btn.setAttribute('aria-label', label);
            btn.title = label;
            input.focus();
            // Garde le curseur à la fin du texte
            const len = input.value.length;
            try { input.setSelectionRange(len, len); } catch { }
        });

        if (input.parentElement.classList.contains('input-group')) {
            // Dans un input-group Bootstrap : bouton collé juste après le champ
            btn.className = 'btn btn-outline-secondary pwd-toggle-btn';
            input.after(btn);
        } else {
            // Sinon : l'œil est placé à l'intérieur du champ, à droite
            const wrap = document.createElement('div');
            wrap.className = 'pwd-field';
            input.parentNode.insertBefore(wrap, input);
            wrap.appendChild(input);
            btn.className = 'pwd-toggle';
            wrap.appendChild(btn);
        }
    }

    const init = () => document.querySelectorAll('input[type="password"]').forEach(addToggle);
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();
