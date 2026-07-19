import { Routes } from '@angular/router';
import { ChatComponent } from './components/chat/chat.component';
import { TicTacToeComponent } from './components/tictactoe/tictactoe.component';

export const routes: Routes = [
  { path: 'chat', component: ChatComponent },
  { path: 'tictactoe', component: TicTacToeComponent },
  { path: '', redirectTo: '/chat', pathMatch: 'full' }
];
