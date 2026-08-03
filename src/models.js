// Couche d'aiguillage des modèles 3D.
//
// Chaque pièce du décor est décrite une fois ici : le fichier .glb attendu, et
// la primitive de repli utilisée tant que ce fichier n'existe pas. Le reste du
// jeu ne demande jamais « une boîte » mais « un bureau d'accueil » — le jour où
// le .glb arrive, il suffit de renseigner son chemin dans ce tableau.
//
// Les cotes sont en mètres et correspondent à docs/prompts-modeles-3d.md.

export const UNIT = 1;      // 1 unité Three.js = 1 mètre
export const W = 8.4;       // largeur du bâtiment
export const D = 5.2;       // profondeur
export const H = 2.75;      // hauteur sous plafond d'un étage
export const SHAFT = 1.5;   // largeur de la cage d'ascenseur

/**
 * `src` à null = pas encore de modèle, on dessine la primitive.
 * `size` sert à mettre le modèle importé à l'échelle attendue par la scène.
 */
export const MODELS = {
  floorModule:  { src: null, size: [W, H, D] },
  shaft:        { src: null, size: [SHAFT, H, D - 0.5] },
  cab:          { src: null, size: [SHAFT - 0.45, 1.5, D - 1.4] },
  lobby:        { src: null, size: [W, H * 0.92, D] },
  roof:         { src: null, size: [W, 0.26, D] },
  buildDeck:    { src: null, size: [W, 1.2, D] },
  crane:        { src: null, size: [10, 14, 1] },
  van:          { src: null, size: [5, 2.3, 2] },
  worker:       { src: null, size: [0.45, 1.7, 0.45] },
};

/** Mobilier par métier : trois pièces suffisent à camper une pièce. */
export const KITS = {
  accueil:          { src: [null, null, null], kind: 'bureau' },
  cameras:          { src: [null, null, null], kind: 'atelier' },
  reseau:           { src: [null, null, null], kind: 'baie' },
  intrusion:        { src: [null, null, null], kind: 'atelier' },
  acces:            { src: [null, null, null], kind: 'bureau' },
  incendie:         { src: [null, null, null], kind: 'atelier' },
  telesurveillance: { src: [null, null, null], kind: 'pc' },
  cyber:            { src: [null, null, null], kind: 'baie' },
  ia:               { src: [null, null, null], kind: 'baie' },
  audit:            { src: [null, null, null], kind: 'bureau' },
};

/** Couleurs de tenue et carnations, partagées avec la version 2D. */
export const SKIN_TONES = [0xf3c9a2, 0xe0a878, 0xc2865a, 0x8d5a34, 0x5c3a21];

/** Charge Three.js à la demande : inutile de peser 670 Ko sur un repli 2D. */
let threePromise = null;

export function loadThree() {
  if (threePromise) return threePromise;
  threePromise = new Promise((resolve, reject) => {
    if (window.THREE) return resolve(window.THREE);
    const tag = document.createElement('script');
    tag.src = './vendor/three.min.js';
    tag.onload = () => (window.THREE ? resolve(window.THREE) : reject(new Error('THREE absent')));
    tag.onerror = () => reject(new Error('chargement de Three.js impossible'));
    document.head.appendChild(tag);
  });
  return threePromise;
}

/** Le rendu 3D n'est tenté que si la machine sait faire du WebGL. */
export function supportsWebGL() {
  try {
    const canvas = document.createElement('canvas');
    return !!(window.WebGLRenderingContext
      && (canvas.getContext('webgl') || canvas.getContext('experimental-webgl')));
  } catch (err) {
    return false;
  }
}

/**
 * Charge les .glb déclarés dans MODELS. Tant qu'aucun `src` n'est renseigné,
 * la fonction rend une table vide et la scène se rabat sur les primitives.
 */
export async function loadModels(THREE) {
  const wanted = Object.entries(MODELS).filter(([, m]) => m.src);
  if (!wanted.length) return {};

  const { GLTFLoader } = await import('../vendor/GLTFLoader.js').catch(() => ({}));
  if (!GLTFLoader) {
    console.warn('GLTFLoader absent : la scène reste sur les primitives.');
    return {};
  }

  const loader = new GLTFLoader();
  const loaded = {};
  await Promise.all(wanted.map(async ([key, m]) => {
    try {
      const gltf = await loader.loadAsync(m.src);
      loaded[key] = gltf.scene;
    } catch (err) {
      console.warn(`Modèle « ${key} » illisible, primitive conservée`, err);
    }
  }));
  return loaded;
}
