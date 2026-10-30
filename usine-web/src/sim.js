const E = require('./econ.js');
function run(minutes, attentionSec) {
  const S = E.newState(); S.cash = 10; let t = 0; const g = E.globals({});
  const marks = [0.5, 1, 2, 3, 5, 10, 15, 20, 30, 45, 60, 90, 120, 180, 240]; let mi = 0; const log = []; const unl = {};
  let buys = 0, firstBuy = null;
  while (t < minutes * 60) {
    S.cash += E.totalIncome(S, g) * attentionSec; t += attentionSec;
    for (let guard = 0; guard < 40; guard++) {
      const opts = E.options(S, g, 1); opts.forEach(o => { if (o.kind === 'unlock') o.payback *= 0.35; }); const b = E.best(opts); if (!b) break;
      let pick = null;
      if (b.cost <= S.cash) pick = b;
      else { const wait = (b.cost - S.cash) / Math.max(1e-9, E.totalIncome(S, g)); if (wait > 15) opts.filter(o => o.cost <= S.cash).forEach(o => { if (!pick || o.payback < pick.payback) pick = o; }); }
      if (!pick) break;
      S.cash -= pick.cost; buys++; if (firstBuy === null) firstBuy = t;
      if (pick.kind === 'unlock') { S.st[pick.i].u = true; unl[E.ST[pick.i].name] = Math.round(t) + 's'; }
      else S.st[pick.i][pick.axis] += 1;
    }
    while (mi < marks.length && t >= marks[mi] * 60) {
      log.push(marks[mi] + ' min : revenu ' + E.totalIncome(S, g).toExponential(2) + '/s, niveaux ' + S.st.map(s => s.u ? s.s + '+' + s.a : '-').join(' ') + ', achats ' + buys);
      mi++;
    }
  }
  return { log, unl, firstBuy };
}
const a = +process.argv[2] || 5;
const r = run(240, a);
console.log('Attention toutes les ' + a + ' s ; 1er achat à ' + r.firstBuy + ' s ; déblocages :', JSON.stringify(r.unl));
console.log(r.log.join('\n'));
