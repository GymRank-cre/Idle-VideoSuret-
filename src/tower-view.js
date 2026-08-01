// Sélection du moteur visuel. La vue CSS reste le filet de sécurité pour les
// navigateurs sans WebGL ou lorsque Three.js ne peut pas être initialisé.

import * as css from './tower.js';
import * as webgl from './tower3d.js';

let active = css;

function canUseWebGL() {
  if (!window.THREE) return false;
  try {
    const canvas = document.createElement('canvas');
    return Boolean(canvas.getContext('webgl2') || canvas.getContext('webgl'));
  } catch (_) {
    return false;
  }
}

export function buildTower(container, state) {
  active = canUseWebGL() ? webgl : css;
  try {
    return active.buildTower(container, state);
  } catch (error) {
    console.warn('Vue 3D indisponible, retour à la tour CSS.', error);
    active = css;
    return active.buildTower(container, state);
  }
}

export const openFloors = css.openFloors;
export function updateTower(state) { active.updateTower(state); }
export function updateBuildSlot(state, container) { active.updateBuildSlot(state, container); }
export function popCoin(id, amount) { active.popCoin(id, amount); }
export function moveCab(id) { active.moveCab(id); }

