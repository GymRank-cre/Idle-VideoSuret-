// Sélection du moteur visuel de la tour.
//
// Le reste du jeu ne parle qu'à ce module : il ignore si la tour est dessinée
// en WebGL ou en CSS. La vue CSS reste le filet de sécurité — navigateur sans
// WebGL, Three.js injoignable, ou `?render=2d` dans l'adresse.
//
// Three.js n'est chargé qu'au moment où la 3D est réellement retenue : sur le
// chemin de repli, les 670 Ko ne sont jamais téléchargés.

import * as css from './tower.js';
import * as webgl from './tower3d.js';
import { loadThree, supportsWebGL } from './models.js';

let active = css;
let pickHandler = null;

export function is3D() {
  return active === webgl;
}

/**
 * Tente de passer en WebGL. Rend une promesse résolue à `true` si la 3D a pris
 * la main. Le jeu reste jouable dans les deux cas : seul l'habillage change.
 */
export function enable3D() {
  if (location.search.includes('render=2d')) return Promise.resolve(false);
  if (!supportsWebGL()) return Promise.resolve(false);

  return loadThree()
    .then((THREE) => {
      webgl.init(THREE);
      if (pickHandler) webgl.setPickHandler(pickHandler);
      active = webgl;
      return true;
    })
    .catch((err) => {
      console.warn('Tour 3D indisponible, rendu CSS conservé', err);
      active = css;
      return false;
    });
}

/** Appelé quand le joueur touche un étage dans la scène 3D. */
export function setPickHandler(fn) {
  pickHandler = fn;
  webgl.setPickHandler(fn);
}

export const openFloors = css.openFloors;

export function buildTower(container, state) {
  try {
    return active.buildTower(container, state);
  } catch (error) {
    // Un échec d'initialisation WebGL ne doit pas emporter la partie.
    console.warn('Vue 3D en défaut, retour à la tour CSS.', error);
    active = css;
    return active.buildTower(container, state);
  }
}

export function updateTower(state) { active.updateTower(state); }
export function updateBuildSlot(state, container) { active.updateBuildSlot(state, container); }
export function popCoin(id, amount) { active.popCoin(id, amount); }
export function moveCab(id) { active.moveCab(id); }

/** Recadre après une remise à zéro : la tour a perdu ses étages. */
export function reframe() {
  if (is3D()) webgl.reframe();
}
