"use client";

const screenshots = [
    { src: "/screenshots/dashboard-dark.png", alt: "Hue Companion dashboard with pinned rooms" },
    { src: "/screenshots/home-light.png", alt: "Home page showing all rooms in light mode" },
    { src: "/screenshots/room-lights-dark.png", alt: "Room detail with individual light controls" },
    { src: "/screenshots/room-detail-light.png", alt: "Room detail in light mode" },
    { src: "/screenshots/scene-builder-dark.png", alt: "Scene Builder with multi-track timeline" },
    { src: "/screenshots/effects-dark.png", alt: "Native Hue effects grid" },
    { src: "/screenshots/settings-dark.png", alt: "Settings with global hotkey configuration" },
    { src: "/screenshots/scene-builder-light.png", alt: "Scene Builder in light mode" },
];

export function Showcase() {
    return (
        <section className="py-24 bg-black overflow-hidden relative">
            <div className="container px-4 md:px-6 mb-12 relative z-10">
                <h2 className="text-3xl md:text-5xl font-bold text-center mb-6">Designed for Windows 11.</h2>
                <p className="text-zinc-400 text-center max-w-2xl mx-auto mb-12">
                    Every pixel is crafted to look and feel native. Dark and light themes included.
                </p>
            </div>

            <div className="flex gap-8 animate-scroll whitespace-nowrap px-4 w-max">
                {/* Double the array for infinite scroll effect */}
                {[...screenshots, ...screenshots].map((shot, i) => (
                    <div key={i} className="inline-block w-[600px] rounded-xl overflow-hidden shadow-2xl border border-white/10 relative group">
                        <div className="absolute inset-0 bg-black/0 group-hover:bg-black/10 transition-colors duration-300" />
                        <img src={shot.src} alt={shot.alt} className="w-full h-auto object-cover" />
                    </div>
                ))}
            </div>

            <style jsx>{`
        @keyframes scroll {
          0% { transform: translateX(0); }
          100% { transform: translateX(calc(-100% / 2 - 2rem)); }
        }
        .animate-scroll {
          animation: scroll 40s linear infinite;
        }
        .animate-scroll:hover {
            animation-play-state: paused;
        }
      `}</style>
        </section>
    );
}
