// Contenu du jeu : services de l'entreprise, améliorations, R&D, contrats, objectifs.
// Aucune logique ici, uniquement des données pures.

/**
 * Chaque service (« étage » de la tour) produit du chiffre d'affaires par cycle.
 * baseCost / growth  : coût du premier poste et croissance géométrique
 * baseRevenue        : CA par cycle et par poste (avant multiplicateurs)
 * baseTime           : durée d'un cycle en secondes
 * managerCost        : prix du chef de service qui automatise le cycle
 */
export const FLOORS = [
  {
    id: 'accueil',
    name: 'Accueil & filtrage visiteurs',
    icon: '🛎️',
    unit: 'agent',
    baseCost: 4,
    growth: 1.07,
    baseRevenue: 1,
    baseTime: 0.6,
    managerCost: 1e3,
    manager: 'Nadia Berthier',
    desc: "Badge visiteur, registre d'entrée, levée de doute au portillon.",
  },
  {
    id: 'cameras',
    name: 'Pose de caméras',
    icon: '📹',
    unit: 'poseur',
    baseCost: 60,
    growth: 1.15,
    baseRevenue: 60,
    baseTime: 3,
    managerCost: 15e3,
    manager: 'Karim Vasseur',
    desc: 'Dômes, bullets, PTZ : nacelle, réglage du champ, mise au point.',
  },
  {
    id: 'reseau',
    name: 'Câblage & réseau IP',
    icon: '🔌',
    unit: 'câbleur',
    baseCost: 720,
    growth: 1.14,
    baseRevenue: 540,
    baseTime: 6,
    managerCost: 100e3,
    manager: 'Élodie Mansart',
    desc: 'Chemins de câbles, switchs PoE, VLAN dédié vidéo, recette réseau.',
  },
  {
    id: 'intrusion',
    name: 'Alarmes intrusion',
    icon: '🚨',
    unit: 'technicien',
    baseCost: 8640,
    growth: 1.13,
    baseRevenue: 4320,
    baseTime: 12,
    managerCost: 500e3,
    manager: 'Bruno Delcourt',
    desc: 'Centrales, détecteurs volumétriques, contacts d\'ouverture, sirènes.',
  },
  {
    id: 'acces',
    name: "Contrôle d'accès & badges",
    icon: '🪪',
    unit: 'intégrateur',
    baseCost: 103680,
    growth: 1.12,
    baseRevenue: 51840,
    baseTime: 24,
    managerCost: 1.2e6,
    manager: 'Sonia Rehault',
    desc: 'Lecteurs, ventouses, gestion des droits, anti-passback, PMR.',
  },
  {
    id: 'incendie',
    name: 'Détection incendie',
    icon: '🔥',
    unit: 'spécialiste',
    baseCost: 1244160,
    growth: 1.11,
    baseRevenue: 622080,
    baseTime: 96,
    managerCost: 10e6,
    manager: 'Yann Prigent',
    desc: 'SSI, détecteurs optiques, désenfumage, essais périodiques.',
  },
  {
    id: 'telesurveillance',
    name: 'Centre de télésurveillance',
    icon: '🖥️',
    unit: 'opérateur',
    baseCost: 14929920,
    growth: 1.10,
    baseRevenue: 7464960,
    baseTime: 384,
    managerCost: 111e6,
    manager: 'Camille Ostrowski',
    desc: 'Levée de doute vidéo 24/7, hypervision, consignes clients, main courante.',
  },
  {
    id: 'cyber',
    name: 'Cybersécurité des flux vidéo',
    icon: '🛡️',
    unit: 'analyste',
    baseCost: 179159040,
    growth: 1.09,
    baseRevenue: 89579520,
    baseTime: 1536,
    managerCost: 555e6,
    manager: 'Théo Nkemba',
    desc: 'Durcissement des NVR, segmentation, chiffrement, journalisation.',
  },
  {
    id: 'ia',
    name: 'Analyse vidéo IA',
    icon: '🤖',
    unit: 'ingénieur',
    baseCost: 2149908480,
    growth: 1.08,
    baseRevenue: 1074954240,
    baseTime: 6144,
    managerCost: 10e9,
    manager: 'Léa Fontaine-Ba',
    desc: 'Franchissement de ligne, objet abandonné, comptage, recherche par attribut.',
  },
  {
    id: 'audit',
    name: 'Audit & formation sûreté',
    icon: '🎓',
    unit: 'consultant',
    baseCost: 25798901760,
    growth: 1.07,
    baseRevenue: 29668737024,
    baseTime: 36864,
    managerCost: 55e9,
    manager: 'Patrick Vaz-Ferreira',
    desc: 'Analyse de risques, schéma directeur sûreté, conformité, plans de formation.',
  },
];

/**
 * Habillage de chaque étage de la tour : couleur de la pièce, mobilier posé
 * contre le mur du fond, et couleurs de tenue du personnel qui s'y déplace.
 * Purement cosmétique — rien ici n'entre dans les calculs.
 */
const SKINS = {
  accueil:          { wall: '#fef3c7', accent: '#f59e0b', decor: ['🪴', '🛋️', '🖥️'], wear: ['#f59e0b', '#fbbf24'] },
  cameras:          { wall: '#dbeafe', accent: '#3b82f6', decor: ['🪜', '📦', '📹'], wear: ['#2563eb', '#60a5fa'] },
  reseau:           { wall: '#dcfce7', accent: '#22c55e', decor: ['🗄️', '🧰', '🔌'], wear: ['#16a34a', '#4ade80'] },
  intrusion:        { wall: '#fee2e2', accent: '#ef4444', decor: ['🔔', '🧰', '🚨'], wear: ['#dc2626', '#f87171'] },
  acces:            { wall: '#ede9fe', accent: '#8b5cf6', decor: ['🚪', '🪪', '🖨️'], wear: ['#7c3aed', '#a78bfa'] },
  incendie:         { wall: '#ffedd5', accent: '#f97316', decor: ['🧯', '🚒', '📋'], wear: ['#ea580c', '#fb923c'] },
  telesurveillance: { wall: '#cffafe', accent: '#06b6d4', decor: ['🖥️', '🖥️', '☕'], wear: ['#0891b2', '#22d3ee'] },
  cyber:            { wall: '#e0e7ff', accent: '#6366f1', decor: ['💻', '🔐', '🗃️'], wear: ['#4f46e5', '#818cf8'] },
  ia:               { wall: '#fae8ff', accent: '#d946ef', decor: ['🖥️', '🤖', '📡'], wear: ['#c026d3', '#e879f9'] },
  audit:            { wall: '#ecfccb', accent: '#84cc16', decor: ['📊', '📚', '🗂️'], wear: ['#65a30d', '#a3e635'] },
};

for (const floor of FLOORS) Object.assign(floor, SKINS[floor.id]);

/** Paliers de postes qui doublent le CA du service. */
export const MILESTONES = [25, 50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000];

/**
 * Améliorations achetées en euros.
 * target : id d'un service, ou 'all' pour un bonus global.
 * kind   : 'revenue' (multiplie le CA) ou 'speed' (divise la durée de cycle).
 * req    : { floor, count } postes nécessaires pour débloquer l'achat.
 */
export const UPGRADES = [];

const UPGRADE_TIERS = [
  { count: 10, costFactor: 250, mult: 3, kind: 'revenue', label: 'Outillage dédié' },
  { count: 25, costFactor: 2500, mult: 3, kind: 'revenue', label: 'Procédure normalisée' },
  { count: 50, costFactor: 25000, mult: 2, kind: 'speed', label: 'Équipe doublée' },
  { count: 100, costFactor: 250000, mult: 4, kind: 'revenue', label: 'Agence régionale' },
];

for (const floor of FLOORS) {
  for (const tier of UPGRADE_TIERS) {
    UPGRADES.push({
      id: `${floor.id}_${tier.count}`,
      name: `${tier.label} — ${floor.name}`,
      icon: floor.icon,
      target: floor.id,
      kind: tier.kind,
      mult: tier.mult,
      cost: floor.baseCost * tier.costFactor,
      req: { floor: floor.id, count: tier.count },
      desc:
        tier.kind === 'speed'
          ? `Durée des cycles divisée par ${tier.mult} sur « ${floor.name} ».`
          : `Chiffre d'affaires multiplié par ${tier.mult} sur « ${floor.name} ».`,
    });
  }
}

UPGRADES.push(
  {
    id: 'global_vehicules',
    name: "Parc de véhicules d'intervention",
    icon: '🚐',
    target: 'all',
    kind: 'revenue',
    mult: 2,
    cost: 5e5,
    req: { floor: 'reseau', count: 10 },
    desc: 'Fourgons équipés : moins de trajets perdus, ×2 sur tous les services.',
  },
  {
    id: 'global_vms',
    name: 'Licence VMS entreprise',
    icon: '🗄️',
    target: 'all',
    kind: 'revenue',
    mult: 2,
    cost: 2.5e7,
    req: { floor: 'acces', count: 10 },
    desc: 'Hyperviseur unique pour tous les sites, ×2 sur tous les services.',
  },
  {
    id: 'global_astreinte',
    name: 'Astreinte 24/7',
    icon: '📟',
    target: 'all',
    kind: 'speed',
    mult: 2,
    cost: 5e9,
    req: { floor: 'telesurveillance', count: 15 },
    desc: 'Les équipes tournent en 3×8 : tous les cycles vont deux fois plus vite.',
  },
  {
    id: 'global_marque',
    name: 'Notoriété nationale',
    icon: '🏆',
    target: 'all',
    kind: 'revenue',
    mult: 5,
    cost: 5e12,
    req: { floor: 'ia', count: 10 },
    desc: 'Appels d\'offres gagnés d\'avance, ×5 sur tous les services.',
  }
);

/**
 * Arbre de R&D, payé en points de R&D gagnés sur les contrats.
 * effect : décrit par la logique dans economy.js / game.js
 */
export const RESEARCH = [
  {
    id: 'proc',
    name: 'Procédures écrites',
    icon: '📋',
    cost: 5,
    req: [],
    desc: '+25 % de chiffre d\'affaires sur tous les services.',
  },
  {
    id: 'indus',
    name: 'Industrialisation des poses',
    icon: '⚙️',
    cost: 40,
    req: ['proc'],
    desc: '+50 % de chiffre d\'affaires sur tous les services.',
  },
  {
    id: 'excellence',
    name: 'Excellence opérationnelle',
    icon: '💎',
    cost: 220,
    req: ['indus'],
    desc: '+100 % de chiffre d\'affaires sur tous les services.',
  },
  {
    id: 'commerce',
    name: 'Service commercial',
    icon: '🤝',
    cost: 8,
    req: [],
    desc: '+50 % sur les récompenses de contrats.',
  },
  {
    id: 'comptes',
    name: 'Direction grands comptes',
    icon: '🏢',
    cost: 35,
    req: ['commerce'],
    desc: 'Les appels d\'offres arrivent deux fois plus souvent.',
  },
  {
    id: 'bureau',
    name: "Bureau d'études",
    icon: '📐',
    cost: 60,
    req: ['commerce'],
    desc: 'Double les points de R&D gagnés sur les contrats.',
  },
  {
    id: 'veille',
    name: 'Veille nocturne',
    icon: '🌙',
    cost: 10,
    req: [],
    desc: 'Revenus hors-ligne plafonnés à 8 h au lieu de 4 h.',
  },
  {
    id: 'pc247',
    name: 'PC sécurité permanent',
    icon: '🕰️',
    cost: 70,
    req: ['veille'],
    desc: 'Hors-ligne : plafond 16 h et rendement porté à 75 %.',
  },
  {
    id: 'renfort',
    name: "Renfort d'équipe",
    icon: '⚡',
    cost: 15,
    req: [],
    desc: 'Débloque le bouton Renfort : ×3 pendant 60 s, 5 min de récupération.',
  },
  {
    id: 'reserve',
    name: 'Réserve opérationnelle',
    icon: '🔋',
    cost: 90,
    req: ['renfort'],
    desc: 'Renfort porté à ×5 pendant 90 s, récupération réduite à 3 min.',
  },
  {
    id: 'qualite',
    name: 'Direction qualité',
    icon: '📈',
    cost: 120,
    req: ['proc'],
    desc: 'Chaque étoile de certification vaut +3 % au lieu de +2 %.',
  },
  {
    id: 'dossier',
    name: 'Dossier de certification',
    icon: '🗂️',
    cost: 300,
    req: ['qualite', 'excellence'],
    desc: '+30 % d\'étoiles gagnées lors d\'une certification.',
  },
];

/**
 * Modèles d'appels d'offres. reward = CA/seconde × payout, versé à la fin.
 * minFloor : index de service minimum débloqué pour que l'offre apparaisse.
 */
export const CONTRACTS = [
  { id: 'pharmacie', name: 'Pharmacie de quartier', icon: '💊', duration: 20, payout: 40, rd: 1, minFloor: 0 },
  { id: 'boulangerie', name: 'Boulangerie du centre', icon: '🥖', duration: 25, payout: 55, rd: 1, minFloor: 0 },
  { id: 'concession', name: 'Concession automobile', icon: '🚗', duration: 45, payout: 120, rd: 2, minFloor: 1 },
  { id: 'entrepot', name: 'Entrepôt logistique', icon: '📦', duration: 60, payout: 180, rd: 2, minFloor: 2 },
  { id: 'ecole', name: 'Groupe scolaire', icon: '🏫', duration: 75, payout: 240, rd: 3, minFloor: 3 },
  { id: 'centre', name: 'Centre commercial', icon: '🛒', duration: 90, payout: 320, rd: 3, minFloor: 4 },
  { id: 'hopital', name: 'Centre hospitalier', icon: '🏥', duration: 120, payout: 480, rd: 4, minFloor: 5 },
  { id: 'stade', name: 'Stade municipal', icon: '🏟️', duration: 150, payout: 640, rd: 5, minFloor: 6 },
  { id: 'aeroport', name: 'Zone aéroportuaire', icon: '✈️', duration: 180, payout: 900, rd: 6, minFloor: 7 },
  { id: 'nucleaire', name: 'Site industriel classé', icon: '☢️', duration: 240, payout: 1400, rd: 8, minFloor: 8 },
];

/** Objectifs : chacun accorde +2 % de chiffre d'affaires global, définitivement. */
export const ACHIEVEMENTS = [
  { id: 'first', name: 'Première affaire', desc: 'Encaisser vos premiers euros.', check: (s, st) => st.lifetimeEarned >= 1 },
  { id: 'cash_1k', name: 'Trésorerie saine', desc: 'Encaisser 1 k€ au total.', check: (s, st) => st.lifetimeEarned >= 1e3 },
  { id: 'cash_1m', name: 'PME reconnue', desc: 'Encaisser 1 M€ au total.', check: (s, st) => st.lifetimeEarned >= 1e6 },
  { id: 'cash_1md', name: 'Groupe régional', desc: 'Encaisser 1 Md€ au total.', check: (s, st) => st.lifetimeEarned >= 1e9 },
  { id: 'cash_1bn', name: 'Leader du marché', desc: 'Encaisser 1 Bn€ au total.', check: (s, st) => st.lifetimeEarned >= 1e12 },
  { id: 'floors_3', name: 'Trois métiers', desc: 'Ouvrir 3 services.', check: (s) => countOpen(s) >= 3 },
  { id: 'floors_6', name: 'Offre complète', desc: 'Ouvrir 6 services.', check: (s) => countOpen(s) >= 6 },
  { id: 'floors_10', name: 'Tour de contrôle', desc: 'Ouvrir les 10 services.', check: (s) => countOpen(s) >= 10 },
  { id: 'cam_25', name: 'Parc installé', desc: '25 poseurs de caméras.', check: (s) => (s.floors.cameras?.count || 0) >= 25 },
  { id: 'cam_100', name: 'Champ de vision', desc: '100 poseurs de caméras.', check: (s) => (s.floors.cameras?.count || 0) >= 100 },
  { id: 'any_200', name: 'Service industriel', desc: '200 postes dans un même service.', check: (s) => maxCount(s) >= 200 },
  { id: 'mgr_1', name: 'Premier chef de service', desc: 'Recruter un chef de service.', check: (s) => countManagers(s) >= 1 },
  { id: 'mgr_5', name: 'Comité de direction', desc: 'Recruter 5 chefs de service.', check: (s) => countManagers(s) >= 5 },
  { id: 'mgr_all', name: 'Entreprise autonome', desc: 'Recruter les 10 chefs de service.', check: (s) => countManagers(s) >= 10 },
  { id: 'ct_1', name: 'Premier contrat', desc: 'Livrer un appel d\'offres.', check: (s, st) => st.contractsDone >= 1 },
  { id: 'ct_10', name: 'Carnet de commandes', desc: 'Livrer 10 appels d\'offres.', check: (s, st) => st.contractsDone >= 10 },
  { id: 'ct_50', name: 'Référencé partout', desc: 'Livrer 50 appels d\'offres.', check: (s, st) => st.contractsDone >= 50 },
  { id: 'rd_5', name: 'Innovation', desc: 'Débloquer 5 travaux de R&D.', check: (s) => Object.keys(s.research).length >= 5 },
  { id: 'cert_1', name: 'Certifié', desc: 'Obtenir une première certification.', check: (s, st) => st.certifications >= 1 },
  { id: 'cert_5', name: 'Référence du secteur', desc: 'Se certifier 5 fois.', check: (s, st) => st.certifications >= 5 },
];

function countOpen(s) {
  return FLOORS.filter((f) => (s.floors[f.id]?.count || 0) > 0).length;
}
function countManagers(s) {
  return FLOORS.filter((f) => s.floors[f.id]?.manager).length;
}
function maxCount(s) {
  return Math.max(0, ...FLOORS.map((f) => s.floors[f.id]?.count || 0));
}
