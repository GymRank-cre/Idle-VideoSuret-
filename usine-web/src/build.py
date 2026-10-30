import base64, re, os
def b64(path, mime):
    return 'data:%s;base64,%s' % (mime, base64.b64encode(open(path,'rb').read()).decode())
t=open('template.html',encoding='utf-8').read()
e=open('econ.js',encoding='utf-8').read().replace("if (typeof module !== 'undefined') module.exports = ECON;\n","")
t=t.replace('/*__ECON__*/',e)
# icônes générées : on remplace les symboles SVG par les images (même identifiants, donc rien d'autre à changer)
MAP={'bill':'billet','star':'etoile','gear':'engrenage','bolt':'eclair','plus':'plus','clip':'commandes','flame':'flamme','lock':'cadenas','crate':'caisse','van':'camionnette','scan':'bouclier'}
for sid,fn in MAP.items():
    pat=re.compile(r'<symbol id="i-%s" viewBox="0 0 64 64">.*?</symbol>'%sid, re.S)
    assert pat.search(t), sid
    t=pat.sub('<symbol id="i-%s" viewBox="0 0 64 64"><image href="%s" width="64" height="64"/></symbol>'%(sid,b64('assets/%s.png'%fn,'image/png')), t, count=1)
t=t.replace('/*__PORTRAITS__*/[]','['+','.join('"%s"'%b64('assets/p-%s.jpg'%n,'image/jpeg') for n in ['karim','lea','marc','ines','paul','sofia','hugo','nadia'])+']')
t=t.replace('/*__TITLE__*/','"%s"'%b64('assets/titre.jpg','image/jpeg'))
open('idle-securite-usine.html','w',encoding='utf-8').write(t)
os.makedirs('srv',exist_ok=True)
open('srv/index.html','w',encoding='utf-8').write('<!doctype html>\n<html lang="fr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover"></head><body>\n'+t+'\n</body></html>')
print(len(t)//1024,'Ko')
