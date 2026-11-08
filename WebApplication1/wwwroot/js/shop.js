// SuperMarché — boutique : panier sans rechargement, tiroir, quantités, défilement des rails.
(() => {
    const drawerEl = document.getElementById('cartDrawer');
    const drawerBody = document.getElementById('cartDrawerBody');

    // ---------- Notifications ----------
    function toast(message, { emoji, error, link } = {}) {
        const zone = document.getElementById('toastZone');
        const el = document.createElement('div');
        el.className = 'shop-toast' + (error ? ' is-error' : '');
        el.innerHTML = `<span class="emo">${emoji || (error ? '⚠️' : '✅')}</span><span></span>`;
        el.lastChild.textContent = message;
        if (link) {
            const a = document.createElement('a');
            a.href = '#'; a.textContent = 'Voir le panier';
            a.addEventListener('click', e => { e.preventDefault(); bootstrap.Offcanvas.getOrCreateInstance(drawerEl).show(); });
            el.appendChild(a);
        }
        zone.appendChild(el);
        setTimeout(() => { el.classList.add('out'); setTimeout(() => el.remove(), 300); }, 2800);
    }

    function setCount(n) {
        document.querySelectorAll('[data-cart-count]').forEach(b => {
            b.textContent = n;
            b.classList.toggle('is-empty', n === 0);
            b.classList.remove('bump'); void b.offsetWidth; b.classList.add('bump');
        });
    }

    // ---------- Tiroir panier ----------
    async function loadDrawer() {
        const res = await fetch(drawerBody.dataset.src, { headers: { 'X-Requested-With': 'fetch' } });
        drawerBody.innerHTML = await res.text();
    }
    drawerEl?.addEventListener('show.bs.offcanvas', loadDrawer);

    // ---------- Formulaires panier (ajout / quantité / suppression) ----------
    document.addEventListener('submit', async e => {
        const form = e.target.closest('form[data-cart]');
        if (!form) return;
        e.preventDefault();
        const btn = form.querySelector('.add-btn, .btn-buy');
        btn?.classList.add('is-loading');
        try {
            const res = await fetch(form.action, {
                method: 'POST', body: new FormData(form),
                headers: { 'X-Requested-With': 'fetch' },
            });
            if (!res.ok) throw new Error(res.status);
            const data = await res.json();
            setCount(data.count);

            if (form.hasAttribute('data-refresh')) { await loadDrawer(); return; }
            if (form.hasAttribute('data-open-drawer') && data.ok) {
                bootstrap.Offcanvas.getOrCreateInstance(drawerEl).show();
            } else {
                toast(data.message, { emoji: data.emoji, error: !data.ok, link: data.ok });
            }
            if (btn && data.ok && btn.classList.contains('add-btn')) {
                btn.classList.add('is-done');
                btn.innerHTML = '<i class="bi bi-check-lg"></i>';
                setTimeout(() => { btn.classList.remove('is-done'); btn.innerHTML = '<i class="bi bi-plus-lg"></i>'; }, 1400);
            }
        } catch {
            form.removeAttribute('data-cart');
            form.submit(); // repli sans JavaScript
        } finally {
            btn?.classList.remove('is-loading');
        }
    });

    // ---------- Boutons +/- ----------
    let autoTimer;
    document.addEventListener('click', e => {
        const step = e.target.closest('[data-step]');
        if (!step) return;
        const input = step.parentElement.querySelector('input[type=number]');
        const min = +(input.min || 0), max = +(input.max || 99);
        input.value = Math.max(min, Math.min(max, (+input.value || 0) + +step.dataset.step));
        if (input.hasAttribute('data-autosubmit')) {
            clearTimeout(autoTimer);
            autoTimer = setTimeout(() => input.form.requestSubmit(), 350);
        }
    });
    document.addEventListener('change', e => {
        if (e.target.matches('input[data-autosubmit]')) e.target.form.requestSubmit();
    });

    // ---------- Rails : flèches de défilement ----------
    document.querySelectorAll('[data-rail]').forEach(rail => {
        const wrap = document.createElement('div');
        wrap.className = 'rail-wrap';
        rail.parentNode.insertBefore(wrap, rail);
        wrap.appendChild(rail);
        const mk = (cls, icon, dir) => {
            const b = document.createElement('button');
            b.type = 'button'; b.className = 'rail-nav ' + cls; b.setAttribute('aria-label', cls === 'prev' ? 'Précédent' : 'Suivant');
            b.innerHTML = `<i class="bi ${icon}"></i>`;
            b.addEventListener('click', () => rail.scrollBy({ left: dir * rail.clientWidth * .8, behavior: 'smooth' }));
            wrap.appendChild(b);
            return b;
        };
        const prev = mk('prev', 'bi-chevron-left', -1), next = mk('next', 'bi-chevron-right', 1);
        const update = () => {
            prev.disabled = rail.scrollLeft < 8;
            next.disabled = rail.scrollLeft + rail.clientWidth >= rail.scrollWidth - 8;
        };
        rail.addEventListener('scroll', update, { passive: true });
        window.addEventListener('resize', update);
        update();
    });
})();
