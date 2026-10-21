"""Petits lexiques FR/EN utilisés par le scoring heuristique."""
import re

STOP = set("""
le la les un une des du de d l et ou mais donc or ni car que qui quoi dont où je tu il elle on nous vous ils elles
me te se ce cet cette ces mon ma mes ton ta tes son sa ses notre votre leur leurs y en à au aux par pour sur sous dans
avec sans est sont suis es été être avoir a ai as ont avait fait faire plus pas ne n c j m t s qu très aussi comme si
the a an and or but so of to in on at by for with from is are was were be been have has had do does did not no it this
that these those i you he she we they me him her us them my your his its our their as if then than also very just
""".split())

# Mots qui accrochent / créent de la tension, de la surprise ou de l'émotion.
POWER = set("""
jamais personne secret erreur vérité mensonge problème danger incroyable énorme impossible choc fou folle dingue
terrible pire meilleur gratuit millions milliards interdit caché piège arnaque honte peur colère exploser exploser
révélé révèle surprenant surprise raison vraiment absolument toujours rien tout seul unique premier dernier
never nobody secret mistake truth lie problem danger incredible huge impossible shocking crazy insane worst best
free million billion banned hidden trap scam shame fear angry exploded revealed surprising reason actually
always nothing everything único
""".split())

OPENERS = [
    r"^le problème", r"^la vérité", r"^voici", r"^stop\b", r"^attention", r"^personne ne", r"^ce que personne",
    r"^la plupart", r"^en fait", r"^imagine", r"^savez-vous", r"^tu sais pourquoi", r"^le secret", r"^la vraie",
    r"^the truth", r"^nobody", r"^here'?s", r"^most people", r"^stop\b", r"^imagine", r"^the problem",
    r"^the secret", r"^the real", r"^what nobody", r"^let me tell",
]
OPENERS_RE = [re.compile(p, re.I) for p in OPENERS]

QUESTION_START = set("""
pourquoi comment quand où combien quel quelle quels quelles est-ce qui que quoi peut-on
why how when where what which who whose can could should would do does did is are will
""".split())

# Début de phrase qui dépend d'un contexte qu'on n'a pas dans le clip.
DANGLING = set("""
et mais donc car puis alors ou ensuite sinon bref
and but so because then or which also
""".split())
ANAPHORIC = set("""
il elle ils elles ça ce cela celui celle ceux
he she they it this that these those
""".split())

# Un clip ou une phrase qui finit sur l'un de ces mots (sans ponctuation) s'arrête au milieu d'une idée.
DANGLING_END = set("""
et ou mais donc car que qui dont de du des d la le les un une à au aux en dans avec pour sur sous par comme si quand
parce alors ni je tu il elle on nous vous ils elles ce cette ces mon ma mes ton ta tes son sa ses
and or but so because that which who whom whose to of the a an in on at by for with from as if when while then
than like is are was were be been am i we you they it he she this these those my your our their his her its
""".split())

FILLERS = set("""
euh heu hum bah ben bon genre voilà
um uh uhm hmm er like
""".split())
FILLER_PHRASES = ["en fait", "du coup", "tu vois", "you know", "i mean", "sort of", "kind of"]

TERMINAL = ".?!…"


def tokens(text: str) -> list[str]:
    return re.findall(r"[\w']+", text.lower(), re.UNICODE)


def content_tokens(text: str) -> list[str]:
    """Mots porteurs de sens, tronqués à 6 lettres (racinisation FR/EN très simple : dormir ~ dormi ~ dorment)."""
    return [t[:6] for t in tokens(text) if t not in STOP and len(t) > 2]
