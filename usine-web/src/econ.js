// ==ECON-START
const ECON = (function () {
  // Chaque poste est un producteur indépendant : on améliore sa VITESSE ou on AJOUTE des pièces par cycle.
  const ST = [
    { id: 'recep', name: 'Réception',   item: 'caisse',          items: 'caisses',          cycle: 1.6, value: 1,     unlock: 0 },
    { id: 'assem', name: 'Assemblage',  item: 'caméra montée',   items: 'caméras montées',  cycle: 3.0, value: 12,     unlock: 180 },
    { id: 'test',  name: 'Test',        item: 'caméra testée',   items: 'caméras testées',  cycle: 4.5, value: 144,    unlock: 14400 },
    { id: 'pack',  name: 'Emballage',   item: 'coffret',         items: 'coffrets',         cycle: 6.0, value: 1728,   unlock: 1152000 },
    { id: 'ship',  name: 'Expédition',  item: 'colis',           items: 'colis',            cycle: 8.0, value: 20736,  unlock: 92160000 },
  ];
  const K = { speedStep: 0.12, gSpeed: 1.19, gAdd: 1.26, costSpeed: 12, costAdd: 6 };
  const MS = [10, 25, 50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000];
  ST.forEach(function (s) {
    s.i0 = s.value / s.cycle;                               // revenu par seconde au niveau 0
    s.cs = Math.max(10, s.i0 * K.costSpeed);                // prix de base de la vitesse
    s.ca = Math.max(7, s.i0 * K.costAdd);                   // prix de base de « Ajouter »
  });

  function msCount(l) { var n = 0; for (var k = 0; k < MS.length; k++) { if (l >= MS[k]) n++; else break; } return n; }
  function nextMs(l) { for (var k = 0; k < MS.length; k++) if (l < MS[k]) return MS[k]; return null; }

  // Bonus permanents (Réputation) -> multiplicateurs
  function globals(rep) {
    rep = rep || {};
    return {
      rev: 1 + 0.10 * (rep.rev || 0),
      spd: 1 + 0.05 * (rep.spd || 0),
      cost: Math.max(0.4, 1 - 0.03 * (rep.cost || 0)),
      order: 1 + 0.15 * (rep.ordr || 0),
      offlineHours: 4 + 2 * (rep.off || 0),
      slots: 1 + (rep.slot || 0),
      turboLen: 60 + 15 * (rep.turbo || 0),
      turboCd: Math.max(90, 240 - 30 * (rep.turbo || 0))
    };
  }
  function itemsPerSec(i, st, g) { return (1 + st.a) * (1 + K.speedStep * st.s) * g.spd / ST[i].cycle; }
  function moneyPerItem(i, st, g) { return ST[i].value * Math.pow(2, msCount(st.s + st.a)) * g.rev; }
  function income(i, st, g) { return st.u ? itemsPerSec(i, st, g) * moneyPerItem(i, st, g) : 0; }
  function totalIncome(S, g) { var t = 0; for (var i = 0; i < ST.length; i++) t += income(i, S.st[i], g); return t; }

  function baseCost(axis, i) { return axis === 's' ? ST[i].cs : ST[i].ca; }
  function growth(axis) { return axis === 's' ? K.gSpeed : K.gAdd; }
  function bulkCost(axis, i, L, k, g) {
    var r = growth(axis);
    return baseCost(axis, i) * g.cost * Math.pow(r, L) * (Math.pow(r, k) - 1) / (r - 1);
  }
  function maxAffordable(axis, i, L, cash, g) {
    var r = growth(axis), c1 = baseCost(axis, i) * g.cost * Math.pow(r, L);
    if (cash < c1) return 0;
    var k = Math.floor(Math.log(1 + cash * (r - 1) / c1) / Math.log(r));
    while (k > 0 && bulkCost(axis, i, L, k, g) > cash) k--;
    return Math.max(0, k);
  }
  function withLevels(st, axis, k) { return { u: st.u, s: st.s + (axis === 's' ? k : 0), a: st.a + (axis === 'a' ? k : 0) }; }

  // Toutes les options d'achat (×k) avec le gain exact de revenu, pour dire ce qui rapporte le plus
  function options(S, g, k) {
    var out = [], base = totalIncome(S, g), nextLocked = -1;
    for (var i = 0; i < ST.length; i++) {
      var st = S.st[i];
      if (!st.u) { if (nextLocked < 0) nextLocked = i; continue; }
      ['s', 'a'].forEach(function (axis) {
        var cost = bulkCost(axis, i, axis === 's' ? st.s : st.a, k, g);
        var after = income(i, withLevels(st, axis, k), g);
        var delta = after - income(i, st, g);
        out.push({ kind: 'up', i: i, axis: axis, k: k, cost: cost, delta: delta, payback: delta > 0 ? cost / delta : Infinity,
                   ms: msCount(st.s + st.a + k) > msCount(st.s + st.a) });
      });
    }
    if (nextLocked >= 0) {
      var cost = ST[nextLocked].unlock * g.cost;
      var delta = income(nextLocked, { u: true, s: 0, a: 0 }, g);
      out.push({ kind: 'unlock', i: nextLocked, axis: 'u', k: 1, cost: cost, delta: delta, payback: cost / delta, ms: false });
    }
    return out;
  }
  function best(opts) {
    var b = null;
    opts.forEach(function (o) { if (o.payback < Infinity && (!b || o.payback < b.payback)) b = o; });
    return b;
  }
  function newState() {
    return { st: ST.map(function (_, i) { return { u: i === 0, s: 0, a: 0 }; }) };
  }
  return { ST: ST, K: K, MS: MS, msCount: msCount, nextMs: nextMs, globals: globals, itemsPerSec: itemsPerSec, moneyPerItem: moneyPerItem,
           income: income, totalIncome: totalIncome, bulkCost: bulkCost, maxAffordable: maxAffordable, options: options, best: best,
           newState: newState, withLevels: withLevels, baseCost: baseCost, growth: growth };
})();
// ==ECON-END
if (typeof module !== 'undefined') module.exports = ECON;
