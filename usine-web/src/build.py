# Assemble la page unique : template.html + econ.js -> ../idle-securite-usine.html
t = open('template.html', encoding='utf-8').read()
e = open('econ.js', encoding='utf-8').read().replace("if (typeof module !== 'undefined') module.exports = ECON;\n", "")
open('../idle-securite-usine.html', 'w', encoding='utf-8').write(t.replace('/*__ECON__*/', e))
