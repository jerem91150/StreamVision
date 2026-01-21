'use client';

import Link from 'next/link';
import {
  ListVideo,
  User,
  Bell,
  Shield,
  Palette,
  CreditCard,
  ChevronRight,
} from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';

const settingsGroups = [
  {
    title: 'Contenu',
    items: [
      {
        icon: ListVideo,
        label: 'Playlists',
        description: 'Gérer vos sources IPTV',
        href: '/settings/playlists',
        color: 'text-orange-400',
        bgColor: 'bg-orange-500/20',
      },
    ],
  },
  {
    title: 'Compte',
    items: [
      {
        icon: User,
        label: 'Profil',
        description: 'Informations personnelles',
        href: '/profile',
        color: 'text-blue-400',
        bgColor: 'bg-blue-500/20',
      },
      {
        icon: CreditCard,
        label: 'Abonnement',
        description: 'Gérer votre forfait',
        href: '/settings/subscription',
        color: 'text-green-400',
        bgColor: 'bg-green-500/20',
      },
    ],
  },
  {
    title: 'Préférences',
    items: [
      {
        icon: Bell,
        label: 'Notifications',
        description: 'Paramètres de notification',
        href: '/settings/notifications',
        color: 'text-yellow-400',
        bgColor: 'bg-yellow-500/20',
      },
      {
        icon: Palette,
        label: 'Apparence',
        description: 'Thème et affichage',
        href: '/settings/appearance',
        color: 'text-purple-400',
        bgColor: 'bg-purple-500/20',
      },
      {
        icon: Shield,
        label: 'Confidentialité',
        description: 'Contrôle parental et sécurité',
        href: '/settings/privacy',
        color: 'text-red-400',
        bgColor: 'bg-red-500/20',
      },
    ],
  },
];

export default function SettingsPage() {
  return (
    <div className="p-6 max-w-4xl mx-auto">
      <h1 className="text-2xl font-bold text-white mb-6">Paramètres</h1>

      <div className="space-y-6">
        {settingsGroups.map((group) => (
          <div key={group.title}>
            <h2 className="text-sm font-medium text-gray-400 mb-3 uppercase tracking-wider">
              {group.title}
            </h2>
            <Card className="bg-gray-900 border-gray-800">
              <CardContent className="p-0 divide-y divide-gray-800">
                {group.items.map((item) => (
                  <Link
                    key={item.href}
                    href={item.href}
                    className="flex items-center gap-4 p-4 hover:bg-gray-800/50 transition-colors"
                  >
                    <div className={`p-2 rounded-lg ${item.bgColor}`}>
                      <item.icon className={`h-5 w-5 ${item.color}`} />
                    </div>
                    <div className="flex-1">
                      <p className="font-medium text-white">{item.label}</p>
                      <p className="text-sm text-gray-400">{item.description}</p>
                    </div>
                    <ChevronRight className="h-5 w-5 text-gray-500" />
                  </Link>
                ))}
              </CardContent>
            </Card>
          </div>
        ))}
      </div>

      {/* App version */}
      <div className="mt-8 text-center text-sm text-gray-500">
        <p>Visiora v0.1.0</p>
      </div>
    </div>
  );
}
