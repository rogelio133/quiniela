import * as THREE from 'https://cdn.jsdelivr.net/npm/three@0.167.0/build/three.module.min.js';

let _frameId = null;
let _renderer = null;
let _observer = null;

function cleanup() {
    if (_frameId)  { cancelAnimationFrame(_frameId); _frameId = null; }
    if (_renderer) { _renderer.dispose(); _renderer = null; }
    if (_observer) { _observer.disconnect(); _observer = null; }
}

function createSoccerBall(size) {
    const group = new THREE.Group();

    // Smooth white sphere — gives the round soccer ball silhouette
    group.add(new THREE.Mesh(
        new THREE.SphereGeometry(size, 20, 16),
        new THREE.MeshStandardMaterial({
            color:     0xffffff,
            roughness: 0.55,
            metalness: 0.06,
        })
    ));

    // Dodecahedron wireframe overlay — 12 pentagonal faces = black patches
    group.add(new THREE.Mesh(
        new THREE.DodecahedronGeometry(size * 1.012, 0),
        new THREE.MeshBasicMaterial({
            color:       0x111111,
            wireframe:   true,
            transparent: true,
            opacity:     0.72,
        })
    ));

    return group;
}

export function initScene() {
    cleanup();

    const canvas = document.getElementById('login-canvas');
    if (!canvas) return;

    const W        = canvas.offsetWidth  || window.innerWidth;
    const H        = canvas.offsetHeight || window.innerHeight;
    const isMobile = window.innerWidth < 768;

    _renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: false });
    _renderer.setPixelRatio(Math.min(devicePixelRatio, 1.5));
    _renderer.setSize(W, H);

    const scene  = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(55, W / H, 0.1, 200);
    camera.position.z = 28;

    // Lighting — key from top-left, rim from bottom-right
    scene.add(new THREE.AmbientLight(0xffffff, 0.45));
    const key = new THREE.PointLight(0xaaccff, 2.4, 100);
    key.position.set(-20, 25, 15);
    scene.add(key);
    const rim = new THREE.PointLight(0xffcc77, 1.2, 80);
    rim.position.set(18, -14, 10);
    scene.add(rim);

    // Soccer balls
    const ballCount = isMobile ? 6 : 11;
    const balls     = [];
    const spread    = 22;

    for (let i = 0; i < ballCount; i++) {
        const size  = 0.55 + Math.random() * 1.45;
        const ball  = createSoccerBall(size);
        const depth = Math.random();   // 0 = far, 1 = close

        ball.position.set(
            (Math.random() - 0.5) * spread,
            (Math.random() - 0.5) * spread * 0.55,
            (depth - 0.5) * 12 - 3
        );

        // Far balls slightly transparent
        ball.children[0].material.transparent = true;
        ball.children[0].material.opacity     = 0.55 + depth * 0.45;
        ball.children[1].material.opacity     = 0.45 + depth * 0.30;

        ball.userData = {
            vx: (Math.random() - 0.5) * 0.033,
            vy: (Math.random() - 0.5) * 0.021,
            rx: (Math.random() - 0.5) * 0.011,
            ry: (Math.random() - 0.5) * 0.014,
        };
        scene.add(ball);
        balls.push(ball);
    }

    // Star field
    const starCount = isMobile ? 60 : 130;
    const starPos   = new Float32Array(starCount * 3);
    for (let i = 0; i < starCount; i++) {
        starPos[i * 3]     = (Math.random() - 0.5) * 55;
        starPos[i * 3 + 1] = (Math.random() - 0.5) * 35;
        starPos[i * 3 + 2] = (Math.random() - 0.5) * 15 - 8;
    }
    const starGeo = new THREE.BufferGeometry();
    starGeo.setAttribute('position', new THREE.BufferAttribute(starPos, 3));
    scene.add(new THREE.Points(starGeo,
        new THREE.PointsMaterial({ color: 0xffffff, size: 0.1, transparent: true, opacity: 0.4 })));

    // Resize handler
    const onResize = () => {
        const w = canvas.offsetWidth  || window.innerWidth;
        const h = canvas.offsetHeight || window.innerHeight;
        _renderer.setSize(w, h);
        camera.aspect = w / h;
        camera.updateProjectionMatrix();
    };
    window.addEventListener('resize', onResize);

    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        _renderer.render(scene, camera);
        return;
    }

    const fovRad    = THREE.MathUtils.degToRad(55 / 2);
    const targetFps = isMobile ? 30 : 60;
    const frameMs   = 1000 / targetFps;
    let   lastTs    = 0;

    function animate(ts) {
        _frameId = requestAnimationFrame(animate);
        if (ts - lastTs < frameMs) return;
        lastTs = ts;

        const halfW = camera.position.z * Math.tan(fovRad) * camera.aspect + 2;
        const halfH = camera.position.z * Math.tan(fovRad) + 2;

        balls.forEach(b => {
            b.position.x += b.userData.vx;
            b.position.y += b.userData.vy;
            b.rotation.x += b.userData.rx;
            b.rotation.y += b.userData.ry;
            if (Math.abs(b.position.x) > halfW) b.userData.vx *= -1;
            if (Math.abs(b.position.y) > halfH) b.userData.vy *= -1;
        });

        _renderer.render(scene, camera);
    }
    animate(0);

    _observer = new MutationObserver(() => {
        if (!document.getElementById('login-canvas')) {
            cleanup();
            window.removeEventListener('resize', onResize);
        }
    });
    _observer.observe(document.body, { childList: true, subtree: true });
}
