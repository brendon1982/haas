import { Routes } from '@angular/router';
import { ChatComponent } from './components/chat/chat.component';
import { TicTacToeComponent } from './components/tictactoe/tictactoe.component';
import { HomeComponent } from './components/home/home.component';

export const routes: Routes = [
  { path: 'home', component: HomeComponent },
  { path: 'chat', component: ChatComponent },
  { path: 'tictactoe', component: TicTacToeComponent },
  { path: '', redirectTo: '/home', pathMatch: 'full' }
];
