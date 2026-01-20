import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Play, Tv, Film, Monitor, Smartphone, Globe, Check, Zap, Shield, Clock, ListVideo, Layers, Settings, Heart } from 'lucide-react';

const features = [
  {
    icon: ListVideo,
    title: 'Support M3U & Xtream',
    description: 'Importez vos playlists M3U ou connectez-vous a votre serveur Xtream Codes',
  },
  {
    icon: Tv,
    title: 'TV en Direct',
    description: 'Regardez vos chaines en direct avec un lecteur HLS performant',
  },
  {
    icon: Film,
    title: 'VOD Integre',
    description: 'Parcourez et lisez vos films et series depuis vos sources',
  },
  {
    icon: Clock,
    title: 'Guide TV (EPG)',
    description: 'Programme TV integre avec support XMLTV pour toutes vos chaines',
  },
];

const plans = [
  {
    id: 'free',
    name: 'Gratuit',
    price: 0,
    description: 'Pour decouvrir StreamVision',
    features: ['1 playlist', 'Lecteur de base', '1 appareil', 'Publicites'],
  },
  {
    id: 'basic',
    name: 'Basic',
    price: 2.99,
    description: 'Pour un usage personnel',
    features: ['3 playlists', 'Lecteur avance', '2 appareils', 'Sans publicites', 'EPG basique'],
    recommended: true,
  },
  {
    id: 'premium',
    name: 'Premium',
    price: 4.99,
    description: 'Pour toute la famille',
    features: [
      'Playlists illimitees',
      'Lecteur 4K HDR',
      '5 appareils',
      'Sans publicites',
      'EPG complet',
      'Favoris & historique',
      'Support prioritaire',
    ],
  },
];

export default function LandingPage() {
  return (
    <div className="min-h-screen bg-background">
      {/* Navigation */}
      <nav className="fixed top-0 left-0 right-0 z-50 bg-background/80 backdrop-blur-lg border-b border-border">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16">
            <div className="flex items-center gap-2">
              <div className="w-10 h-10 bg-primary rounded-lg flex items-center justify-center">
                <Play className="w-6 h-6 text-white" fill="white" />
              </div>
              <span className="text-xl font-bold text-white">StreamVision</span>
            </div>
            <div className="flex items-center gap-4">
              <Link href="/login">
                <Button variant="ghost">Connexion</Button>
              </Link>
              <Link href="/register">
                <Button>Commencer</Button>
              </Link>
            </div>
          </div>
        </div>
      </nav>

      {/* Hero Section */}
      <section className="relative pt-32 pb-20 px-4 overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-b from-primary/10 via-transparent to-transparent" />
        <div className="max-w-7xl mx-auto text-center relative">
          <h1 className="text-4xl sm:text-5xl md:text-6xl font-bold text-white mb-6">
            Le lecteur IPTV
            <span className="text-primary"> nouvelle generation</span>
          </h1>
          <p className="text-xl text-muted-foreground max-w-2xl mx-auto mb-8">
            Connectez vos sources IPTV (M3U, Xtream Codes) et profitez d&apos;une
            interface moderne pour regarder TV, films et series sur tous vos appareils.
          </p>
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <Link href="/register">
              <Button size="xl" className="gap-2">
                <Play className="w-5 h-5" />
                Essayer gratuitement
              </Button>
            </Link>
            <Link href="#pricing">
              <Button size="xl" variant="outline">
                Voir les offres
              </Button>
            </Link>
          </div>

          {/* Device mockup */}
          <div className="mt-16 relative">
            <div className="bg-card rounded-2xl border border-border p-4 max-w-4xl mx-auto shadow-2xl">
              <div className="aspect-video bg-gradient-to-br from-card-hover to-background rounded-lg flex items-center justify-center">
                <div className="text-center">
                  <div className="w-20 h-20 bg-primary/20 rounded-full flex items-center justify-center mx-auto mb-4">
                    <Play className="w-10 h-10 text-primary" />
                  </div>
                  <p className="text-muted-foreground">Interface StreamVision</p>
                </div>
              </div>
            </div>
            {/* Floating devices */}
            <div className="absolute -left-4 top-1/2 -translate-y-1/2 hidden lg:block">
              <div className="bg-card rounded-xl border border-border p-3 shadow-xl">
                <Smartphone className="w-8 h-8 text-primary" />
              </div>
            </div>
            <div className="absolute -right-4 top-1/3 hidden lg:block">
              <div className="bg-card rounded-xl border border-border p-3 shadow-xl">
                <Monitor className="w-8 h-8 text-primary" />
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* How it works */}
      <section className="py-20 px-4 bg-card/30">
        <div className="max-w-7xl mx-auto">
          <div className="text-center mb-16">
            <h2 className="text-3xl font-bold text-white mb-4">
              Comment ca marche ?
            </h2>
            <p className="text-muted-foreground max-w-xl mx-auto">
              StreamVision est un lecteur multimedia. Vous apportez vos propres sources IPTV,
              nous fournissons l&apos;interface.
            </p>
          </div>
          <div className="grid md:grid-cols-3 gap-8 max-w-4xl mx-auto">
            <div className="text-center">
              <div className="w-16 h-16 bg-primary/10 rounded-full flex items-center justify-center mx-auto mb-4">
                <span className="text-2xl font-bold text-primary">1</span>
              </div>
              <h3 className="text-lg font-semibold text-white mb-2">Creez un compte</h3>
              <p className="text-muted-foreground text-sm">
                Inscrivez-vous gratuitement en quelques secondes
              </p>
            </div>
            <div className="text-center">
              <div className="w-16 h-16 bg-primary/10 rounded-full flex items-center justify-center mx-auto mb-4">
                <span className="text-2xl font-bold text-primary">2</span>
              </div>
              <h3 className="text-lg font-semibold text-white mb-2">Ajoutez vos sources</h3>
              <p className="text-muted-foreground text-sm">
                Importez votre playlist M3U ou connectez votre serveur Xtream
              </p>
            </div>
            <div className="text-center">
              <div className="w-16 h-16 bg-primary/10 rounded-full flex items-center justify-center mx-auto mb-4">
                <span className="text-2xl font-bold text-primary">3</span>
              </div>
              <h3 className="text-lg font-semibold text-white mb-2">Profitez !</h3>
              <p className="text-muted-foreground text-sm">
                Regardez votre contenu avec notre interface moderne
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* Features Section */}
      <section className="py-20 px-4">
        <div className="max-w-7xl mx-auto">
          <div className="text-center mb-16">
            <h2 className="text-3xl font-bold text-white mb-4">
              Fonctionnalites du lecteur
            </h2>
            <p className="text-muted-foreground max-w-xl mx-auto">
              Un lecteur IPTV complet avec toutes les fonctionnalites
              que vous attendez d&apos;une application moderne.
            </p>
          </div>
          <div className="grid md:grid-cols-2 lg:grid-cols-4 gap-6">
            {features.map((feature) => (
              <Card key={feature.title} className="bg-card/50 border-border hover:border-primary/50 transition-colors">
                <CardHeader>
                  <div className="w-12 h-12 bg-primary/10 rounded-lg flex items-center justify-center mb-4">
                    <feature.icon className="w-6 h-6 text-primary" />
                  </div>
                  <CardTitle className="text-lg">{feature.title}</CardTitle>
                </CardHeader>
                <CardContent>
                  <CardDescription>{feature.description}</CardDescription>
                </CardContent>
              </Card>
            ))}
          </div>
        </div>
      </section>

      {/* Why StreamVision */}
      <section className="py-20 px-4 bg-card/30">
        <div className="max-w-7xl mx-auto">
          <div className="grid lg:grid-cols-2 gap-12 items-center">
            <div>
              <h2 className="text-3xl font-bold text-white mb-6">
                Pourquoi choisir StreamVision ?
              </h2>
              <div className="space-y-6">
                <div className="flex gap-4">
                  <div className="w-12 h-12 bg-primary/10 rounded-lg flex items-center justify-center shrink-0">
                    <Zap className="w-6 h-6 text-primary" />
                  </div>
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-1">Lecteur performant</h3>
                    <p className="text-muted-foreground">
                      Support HLS, gestion du buffer optimisee, streaming fluide meme sur connexion lente.
                    </p>
                  </div>
                </div>
                <div className="flex gap-4">
                  <div className="w-12 h-12 bg-primary/10 rounded-lg flex items-center justify-center shrink-0">
                    <Shield className="w-6 h-6 text-primary" />
                  </div>
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-1">Vos donnees protegees</h3>
                    <p className="text-muted-foreground">
                      Vos identifiants de playlist sont chiffres. Nous ne stockons pas vos contenus.
                    </p>
                  </div>
                </div>
                <div className="flex gap-4">
                  <div className="w-12 h-12 bg-primary/10 rounded-lg flex items-center justify-center shrink-0">
                    <Globe className="w-6 h-6 text-primary" />
                  </div>
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-1">Multi-plateforme</h3>
                    <p className="text-muted-foreground">
                      Web, Android, iOS, macOS, Windows. Vos favoris synchronises partout.
                    </p>
                  </div>
                </div>
              </div>
            </div>
            <div className="bg-card rounded-2xl border border-border p-8">
              <div className="grid grid-cols-2 gap-4">
                <div className="bg-background rounded-xl p-6 text-center">
                  <div className="w-12 h-12 bg-primary/10 rounded-full flex items-center justify-center mx-auto mb-3">
                    <Layers className="w-6 h-6 text-primary" />
                  </div>
                  <div className="text-sm text-muted-foreground">Formats M3U, M3U8, Xtream</div>
                </div>
                <div className="bg-background rounded-xl p-6 text-center">
                  <div className="w-12 h-12 bg-primary/10 rounded-full flex items-center justify-center mx-auto mb-3">
                    <Monitor className="w-6 h-6 text-primary" />
                  </div>
                  <div className="text-sm text-muted-foreground">Jusqu&apos;a 4K HDR</div>
                </div>
                <div className="bg-background rounded-xl p-6 text-center">
                  <div className="w-12 h-12 bg-primary/10 rounded-full flex items-center justify-center mx-auto mb-3">
                    <Settings className="w-6 h-6 text-primary" />
                  </div>
                  <div className="text-sm text-muted-foreground">EPG XMLTV integre</div>
                </div>
                <div className="bg-background rounded-xl p-6 text-center">
                  <div className="w-12 h-12 bg-primary/10 rounded-full flex items-center justify-center mx-auto mb-3">
                    <Heart className="w-6 h-6 text-primary" />
                  </div>
                  <div className="text-sm text-muted-foreground">Favoris & historique</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* Pricing Section */}
      <section id="pricing" className="py-20 px-4">
        <div className="max-w-7xl mx-auto">
          <div className="text-center mb-16">
            <h2 className="text-3xl font-bold text-white mb-4">
              Choisissez votre formule
            </h2>
            <p className="text-muted-foreground max-w-xl mx-auto">
              Des tarifs simples pour acceder a notre lecteur IPTV.
              Vous gerez vos propres sources de contenu.
            </p>
          </div>
          <div className="grid md:grid-cols-3 gap-8 max-w-5xl mx-auto">
            {plans.map((plan) => (
              <Card
                key={plan.id}
                className={`relative ${
                  plan.recommended
                    ? 'border-primary bg-card shadow-xl shadow-primary/10'
                    : 'bg-card/50 border-border'
                }`}
              >
                {plan.recommended && (
                  <div className="absolute -top-3 left-1/2 -translate-x-1/2">
                    <span className="bg-primary text-white text-xs font-semibold px-3 py-1 rounded-full">
                      Recommande
                    </span>
                  </div>
                )}
                <CardHeader className="text-center pb-2">
                  <CardTitle className="text-xl">{plan.name}</CardTitle>
                  <CardDescription>{plan.description}</CardDescription>
                </CardHeader>
                <CardContent className="text-center">
                  <div className="mb-6">
                    <span className="text-4xl font-bold text-white">{plan.price}€</span>
                    {plan.price > 0 && (
                      <span className="text-muted-foreground">/mois</span>
                    )}
                  </div>
                  <ul className="space-y-3 text-left mb-6">
                    {plan.features.map((feature) => (
                      <li key={feature} className="flex items-center gap-2">
                        <Check className="w-5 h-5 text-success shrink-0" />
                        <span className="text-sm text-muted-foreground">{feature}</span>
                      </li>
                    ))}
                  </ul>
                  <Link href="/register" className="block">
                    <Button
                      className="w-full"
                      variant={plan.recommended ? 'default' : 'outline'}
                    >
                      {plan.price === 0 ? 'Commencer' : 'Choisir'}
                    </Button>
                  </Link>
                </CardContent>
              </Card>
            ))}
          </div>
        </div>
      </section>

      {/* Disclaimer */}
      <section className="py-12 px-4 bg-card/30">
        <div className="max-w-3xl mx-auto text-center">
          <p className="text-sm text-muted-foreground">
            <strong className="text-white">Important :</strong> StreamVision est un lecteur multimedia.
            Nous ne fournissons aucun contenu IPTV (chaines, films, series).
            Vous etes responsable de la legalite des sources que vous utilisez avec notre application.
          </p>
        </div>
      </section>

      {/* CTA Section */}
      <section className="py-20 px-4">
        <div className="max-w-3xl mx-auto text-center">
          <h2 className="text-3xl font-bold text-white mb-4">
            Pret a essayer ?
          </h2>
          <p className="text-muted-foreground mb-8">
            Creez votre compte gratuitement et decouvrez StreamVision.
            Ajoutez vos playlists et profitez d&apos;une experience de visionnage optimale.
          </p>
          <Link href="/register">
            <Button size="xl" className="gap-2">
              <Play className="w-5 h-5" />
              Creer mon compte gratuit
            </Button>
          </Link>
        </div>
      </section>

      {/* Footer */}
      <footer className="border-t border-border py-12 px-4">
        <div className="max-w-7xl mx-auto">
          <div className="flex flex-col md:flex-row justify-between items-center gap-4">
            <div className="flex items-center gap-2">
              <div className="w-8 h-8 bg-primary rounded-lg flex items-center justify-center">
                <Play className="w-4 h-4 text-white" fill="white" />
              </div>
              <span className="font-semibold text-white">StreamVision</span>
            </div>
            <div className="flex gap-6 text-sm text-muted-foreground">
              <Link href="/terms" className="hover:text-white transition-colors">
                Conditions
              </Link>
              <Link href="/privacy" className="hover:text-white transition-colors">
                Confidentialite
              </Link>
              <Link href="/contact" className="hover:text-white transition-colors">
                Contact
              </Link>
            </div>
            <p className="text-sm text-muted-foreground">
              © 2024 StreamVision. Tous droits reserves.
            </p>
          </div>
        </div>
      </footer>
    </div>
  );
}
