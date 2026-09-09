/*
   Folder Hub — movimento da página.

   Sem framework e sem CDN. Duas coisas apenas:

   1. Revelar ao rolar, com IntersectionObserver. A primeira versão fazia isso
      com scroll-driven animations do CSS, que só existem no Chrome e no Edge —
      no Firefox e no Safari a página inteira ficava invisível.

   2. Um leve paralaxe na janela do app.

   O que NÃO existe aqui, de propósito: interceptar a roda do mouse para
   interpolar a rolagem (o efeito tipo Lenis). Foi tentado e saiu pior: o
   trackpad já manda inércia própria, então a interpolação vira uma segunda
   camada de suavização e o resultado é atraso, não fluidez. A rolagem nativa
   do Windows já é suave; o que faltava era ela não ser interrompida por
   trabalho caro a cada quadro.

   A classe `js` é adicionada no <head> antes da primeira pintura. Sem script —
   ou com "menos movimento" ligado no sistema — nada fica escondido.
*/
(() => {
  'use strict';

  const root = document.documentElement;
  if (!root.classList.contains('js')) return;

  /* ------------------------------------------------ revelar ao rolar */

  const groups = document.querySelectorAll('[data-reveal]');

  for (const group of groups) {
    group.querySelectorAll('[data-stagger] > *').forEach((child, i) => {
      child.style.setProperty('--delay', `${Math.min(i, 8) * 70}ms`);
    });
  }

  const observer = new IntersectionObserver((entries) => {
    for (const entry of entries) {
      if (!entry.isIntersecting) continue;

      entry.target.classList.add('is-in');
      observer.unobserve(entry.target);   // revela uma vez só
    }
  }, {
    rootMargin: '0px 0px -10% 0px',
    threshold: 0.05
  });

  for (const group of groups) observer.observe(group);

  /* ------------------------------------------------ topo e paralaxe */

  const mock = document.querySelector('[data-parallax]');

  let queued = false;

  function onScroll() {
    // O evento de scroll dispara mais vezes do que a tela desenha. Sem esta
    // porta, escreveríamos o transform várias vezes por quadro à toa.
    if (queued) return;
    queued = true;

    requestAnimationFrame(() => {
      queued = false;

      const y = window.scrollY;
      root.classList.toggle('at-top', y < 40);

      if (mock) {
        const shift = Math.min(y, 600) * -0.06;
        mock.style.transform = `translate3d(0, ${shift.toFixed(1)}px, 0)`;
      }
    });
  }

  addEventListener('scroll', onScroll, { passive: true });
  onScroll();
})();
