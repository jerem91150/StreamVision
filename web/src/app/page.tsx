import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Play, Tv, Film, Monitor, Smartphone, Globe, Check, Zap, Shield, Clock } from 'lucide-react';

const features = [
  {
    icon: Tv,
    title: 'TV en Direct',
    description: 'Des milliers de chaines du monde entier en qualite HD',
  },
  {
    icon: Film,
    title: 'Films & Series',
    description: 'Catalogue VOD avec les derniers films et series',
  },
  {
    icon: Clock,
    title: 'Replay & Catchup',
    description: 'Rattrapez vos programmes jusqu\'a 7 jours en arriere',
  },
  {
    icon: Globe,
    title: 'Multi-plateforme',
    description: 'Regardez sur tous vos appareils, partout',
  },
];

const plans = [
  {
    id: 'free',
    name: 'Gratuit',
    price: 0,
    description: 'Pour decouvrir StreamVision',
    features: ['100 chaines TV', 'Qualite SD', '1 appareil', 'Publicites'],
  },
  {
    id: 'basic',
    name: 'Basic',
    price: 2.99,
    description: 'Pour un usage personnel',
    features: ['Toutes les chaines', 'Qualite HD', '2 appareils', 'Sans publicites', 'Replay 3 jours'],
    recommended: true,
  },
  {
    id: 'premium',
    name: 'Premium',
    price: 4.99,
    description: 'Pour toute la famille',
    features: [
      'Toutes les chaines',
      'Qualite 4K',
      '5 appareils',
      'Sans publicites',
      'Replay 7 jours',
      'EPG complet',
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
            Votre univers de
            <span className="text-primary"> streaming</span>
          </h1>
          <p className="text-xl text-muted-foreground max-w-2xl mx-auto mb-8">
            Regardez vos chaines TV preferees, films et series en streaming.
            Interface moderne, EPG integre, disponible sur tous vos appareils.
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

      {/* Features Section */}
      <section className="py-20 px-4 bg-card/30">
        <div className="max-w-7xl mx-auto">
          <div className="text-center mb-16">
            <h2 className="text-3xl font-bold text-white mb-4">
              Tout ce dont vous avez besoin
            </h2>
            <p className="text-muted-foreground max-w-xl mx-auto">
              Une experience de streaming complete avec toutes les fonctionnalites
              que vous attendez et plus encore.
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
      <section className="py-20 px-4">
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
                    <h3 className="text-lg font-semibold text-white mb-1">Ultra rapide</h3>
                    <p className="text-muted-foreground">
                      Chargement instantane, pas de buffering, streaming fluide en toute circonstance.
                    </p>
                  </div>
                </div>
                <div className="flex gap-4">
                  <div className="w-12 h-12 bg-primary/10 rounded-lg flex items-center justify-center shrink-0">
                    <Shield className="w-6 h-6 text-primary" />
                  </div>
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-1">Securise</h3>
                    <p className="text-muted-foreground">
                      Vos donnees sont protegees. Connexion chiffree, pas de tracking.
                    </p>
                  </div>
                </div>
                <div className="flex gap-4">
                  <div className="w-12 h-12 bg-primary/10 rounded-lg flex items-center justify-center shrink-0">
                    <Globe className="w-6 h-6 text-primary" />
                  </div>
                  <div>
                    <h3 className="text-lg font-semibold text-white mb-1">Synchronise</h3>
                    <p className="text-muted-foreground">
                      Vos favoris et historique synchronises sur tous vos appareils.
                    </p>
                  </div>
                </div>
              </div>
            </div>
            <div className="bg-card rounded-2xl border border-border p-8">
              <div className="grid grid-cols-2 gap-4">
                <div className="bg-background rounded-xl p-6 text-center">
                  <div className="text-4xl font-bold text-primary mb-2">10K+</div>
                  <div className="text-sm text-muted-foreground">Chaines</div>
                </div>
                <div className="bg-background rounded-xl p-6 text-center">
                  <div className="text-4xl font-bold text-primary mb-2">50K+</div>
                  <div className="text-sm text-muted-foreground">Films & Series</div>
                </div>
                <div className="bg-background rounded-xl p-6 text-center">
                  <div className="text-4xl font-bold text-primary mb-2">4K</div>
                  <div className="text-sm text-muted-foreground">Qualite max</div>
                </div>
                <div className="bg-background rounded-xl p-6 text-center">
                  <div className="text-4xl font-bold text-primary mb-2">24/7</div>
                  <div className="text-sm text-muted-foreground">Disponibilite</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* Pricing Section */}
      <section id="pricing" className="py-20 px-4 bg-card/30">
        <div className="max-w-7xl mx-auto">
          <div className="text-center mb-16">
            <h2 className="text-3xl font-bold text-white mb-4">
              Choisissez votre offre
            </h2>
            <p className="text-muted-foreground max-w-xl mx-auto">
              Des tarifs simples et transparents. Commencez gratuitement,
              evoluez selon vos besoins.
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

      {/* CTA Section */}
      <section className="py-20 px-4">
        <div className="max-w-3xl mx-auto text-center">
          <h2 className="text-3xl font-bold text-white mb-4">
            Pret a commencer ?
          </h2>
          <p className="text-muted-foreground mb-8">
            Rejoignez des milliers d'utilisateurs satisfaits.
            Inscrivez-vous gratuitement et profitez de StreamVision des maintenant.
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
