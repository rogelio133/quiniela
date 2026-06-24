import * as THREE from 'https://cdn.jsdelivr.net/npm/three@0.167.0/build/three.module.min.js';

const _scenes = new Map();

export function initBall(canvasId) {
    if (_scenes.has(canvasId)) return;

    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    const SIZE = 56;
    const renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: true });
    renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
    renderer.setSize(SIZE, SIZE);

    const scene  = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(45, 1, 0.1, 100);
    camera.position.z = 3.2;

    scene.add(new THREE.AmbientLight(0xffffff, 0.5));
    const dir = new THREE.DirectionalLight(0xffffff, 1.5);
    dir.position.set(2, 3, 4);
    scene.add(dir);
    const rim = new THREE.DirectionalLight(0x6699ff, 0.5);
    rim.position.set(-3, -2, -3);
    scene.add(rim);

    // White soccer ball (icosahedron)
    const ball = new THREE.Mesh(
        new THREE.IcosahedronGeometry(1, 1),
        new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.45, metalness: 0.08 })
    );
    scene.add(ball);

    // Dark wireframe overlay (simulates ball patches)
    const wire = new THREE.Mesh(
        new THREE.IcosahedronGeometry(1.02, 1),
        new THREE.MeshBasicMaterial({ color: 0x111111, wireframe: true, transparent: true, opacity: 0.22 })
    );
    scene.add(wire);

    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        renderer.render(scene, camera);
        _scenes.set(canvasId, { renderer, frameId: null });
        return;
    }

    let frameId;
    (function animate() {
        frameId = requestAnimationFrame(animate);
        ball.rotation.y += 0.024;
        ball.rotation.x += 0.009;
        wire.rotation.copy(ball.rotation);
        renderer.render(scene, camera);
    })();

    _scenes.set(canvasId, { renderer, frameId });
}

export function disposeBall(canvasId) {
    const s = _scenes.get(canvasId);
    if (!s) return;
    if (s.frameId) cancelAnimationFrame(s.frameId);
    s.renderer.dispose();
    _scenes.delete(canvasId);
}
