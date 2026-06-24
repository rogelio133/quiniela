import * as THREE from 'https://cdn.jsdelivr.net/npm/three@0.167.0/build/three.module.min.js';

const _scenes = new Map();

export function initTrophy(canvasId) {
    if (_scenes.has(canvasId)) return;

    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    const SIZE = 56;
    const renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: true });
    renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
    renderer.setSize(SIZE, SIZE);

    const scene  = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(42, 1, 0.1, 100);
    camera.position.set(0, 0.2, 4);

    // Lighting for gold material
    scene.add(new THREE.AmbientLight(0xfff8e7, 0.55));
    const key = new THREE.DirectionalLight(0xffd700, 2.2);
    key.position.set(2, 5, 3);
    scene.add(key);
    const rim = new THREE.DirectionalLight(0xff8c00, 0.9);
    rim.position.set(-3, -1, -2);
    scene.add(rim);
    const fill = new THREE.DirectionalLight(0xffffff, 0.4);
    fill.position.set(0, 3, 5);
    scene.add(fill);

    // Trophy cup shape via LatheGeometry (r, y) from bottom to top
    const pts = [
        new THREE.Vector2(0.38, -1.00),  // base outer edge
        new THREE.Vector2(0.40, -0.93),
        new THREE.Vector2(0.38, -0.86),  // base top
        new THREE.Vector2(0.10, -0.78),  // base to stem
        new THREE.Vector2(0.09, -0.55),  // stem
        new THREE.Vector2(0.09, -0.10),  // stem top
        new THREE.Vector2(0.14, -0.02),  // cup floor
        new THREE.Vector2(0.38, 0.28),   // cup widens
        new THREE.Vector2(0.62, 0.62),
        new THREE.Vector2(0.68, 0.84),   // cup widest
        new THREE.Vector2(0.60, 0.96),   // rim
        new THREE.Vector2(0.48, 1.00),   // rim top
    ];

    const geo = new THREE.LatheGeometry(pts, 28);
    const mat = new THREE.MeshStandardMaterial({
        color:    0xFFD700,
        metalness: 0.88,
        roughness: 0.10,
    });
    const trophy = new THREE.Mesh(geo, mat);
    trophy.scale.setScalar(0.72);
    scene.add(trophy);

    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        renderer.render(scene, camera);
        _scenes.set(canvasId, { renderer, frameId: null });
        return;
    }

    let frameId;
    (function animate() {
        frameId = requestAnimationFrame(animate);
        trophy.rotation.y += 0.019;
        renderer.render(scene, camera);
    })();

    _scenes.set(canvasId, { renderer, frameId });
}

export function disposeTrophy(canvasId) {
    const s = _scenes.get(canvasId);
    if (!s) return;
    if (s.frameId) cancelAnimationFrame(s.frameId);
    s.renderer.dispose();
    _scenes.delete(canvasId);
}
