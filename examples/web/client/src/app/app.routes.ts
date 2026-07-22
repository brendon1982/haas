import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: 'home', loadComponent: () => import('./components/home/home.component').then(m => m.HomeComponent) },
  { path: 'chat', loadComponent: () => import('./components/chat/chat.component').then(m => m.ChatComponent) },
  { path: 'tictactoe', loadComponent: () => import('./components/tictactoe/tictactoe.component').then(m => m.TicTacToeComponent) },
  { path: '', redirectTo: '/home', pathMatch: 'full' }
];
