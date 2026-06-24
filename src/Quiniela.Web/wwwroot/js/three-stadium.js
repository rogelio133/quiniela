import * as THREE from 'https://cdn.jsdelivr.net/npm/three@0.167.0/build/three.module.min.js';

let _active = null;

export function initStadium(canvasId) {
    disposeStadium();

    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    // Use the parent container width; fall back to canvas attribute width
    const parent = canvas.parentElement;
    const W = (parent?.clientWidth  || canvas.width  || 400);
    const H = (canvas.height || 140);
    const isMobile = window.innerWidth < 641;

    const renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: false });
    renderer.setPixelRatio(Math.min(devicePixelRatio, isMobile ? 1 : 1.5));
    renderer.setSize(W, H, false);   // false = don't touch CSS size

    const scene  = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(55, W / H, 0.1, 200);
    camera.position.set(0, 14, 22);
    camera.lookAt(0, 0, 0);

    // Grass field
    const groundMat = new THREE.MeshStandardMaterial({ color: 0x1a5c2a, roughness: 0.9 });
    const ground    = new THREE.Mesh(new THREE.PlaneGeometry(38, 24), groundMat);
    ground.rotation.x = -Math.PI / 2;
    scene.add(ground);

    // Field markings
    const lineMat = new THREE.MeshBasicMaterial({
        color: 0xffffff, side: THREE.DoubleSide, transparent: true, opacity: 0.38,
    });
    const circle = new THREE.Mesh(new THREE.RingGeometry(3.4, 3.6, 48), lineMat);
    circle.rotation.x = -Math.PI / 2;
    circle.position.y = 0.01;
    scene.add(circle);

    const dot = new THREE.Mesh(
        new THREE.CircleGeometry(0.18, 12),
        new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.45 })
    );
    dot.rotation.x = -Math.PI / 2;
    dot.position.y = 0.01;
    scene.add(dot);

    // Ambient
    scene.add(new THREE.AmbientLight(0x1a2a44, 0.7));

    // Spotlights
    const spotCount = isMobile ? 2 : 4;
    const corners   = [
        { x: -16, z: -10 }, { x: 16, z: -10 },
        { x: -16, z:  10 }, { x: 16, z:  10 },
    ].slice(0, spotCount);

    const spots = corners.map(({ x, z }, i) => {
        const sp = new THREE.SpotLight(0xfff5dd, isMobile ? 1.4 : 1.8, 70, Math.PI / 7, 0.35, 1.2);
        sp.position.set(x, 20, z);
        sp.target.position.set(0, 0, 0);
        scene.add(sp);
        scene.add(sp.target);
        return { light: sp, phase: (i * Math.PI) / 2 };
    });

    // Resize: update renderer to match parent width
    const onResize = () => {
        if (!canvas.parentElement) return;
        const w = canvas.parentElement.clientWidth || W;
        renderer.setSize(w, H, false);
        camera.aspect = w / H;
        camera.updateProjectionMatrix();
    };
    window.addEventListener('resize', onResize);

    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        renderer.render(scene, camera);
        _active = { renderer, frameId: null, onResize };
        return;
    }

    let frameId, t = 0;
    (function animate() {
        frameId = requestAnimationFrame(animate);
        t += 0.006;
        spots.forEach(s => {
            s.light.target.position.x = Math.sin(t + s.phase) * 5;
            s.light.target.position.z = Math.cos(t * 0.65 + s.phase) * 3.5;
            s.light.target.updateMatrixWorld();
        });
        renderer.render(scene, camera);
    })();

    _active = { renderer, frameId, onResize };
}

export function disposeStadium() {
    if (!_active) return;
    if (_active.frameId)  cancelAnimationFrame(_active.frameId);
    if (_active.onResize) window.removeEventListener('resize', _active.onResize);
    _active.renderer.dispose();
    _active = null;
}
