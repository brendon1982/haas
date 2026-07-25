import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';
import { BaseSignalRService } from './base-signalr.service';

export interface TicTacToeState {
  board: string[];
  winner: 'X' | 'O' | null;
  isDraw: boolean;
  isGameOver: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class TicTacToeSignalRService extends BaseSignalRService {
  public boardUpdated$ = new Subject<TicTacToeState>();

  constructor() {
    super('http://localhost:5000/tictactoeHub', 'Tic-Tac-Toe');

    this.hubConnection.on('BoardUpdated', (state: TicTacToeState) => {
      this.boardUpdated$.next(state);
    });
  }

  protected override onConnected(): void {
    // Initial state request
    this.resetGame();
  }

  public sendMove(position: number): void {
    this.hubConnection.invoke('SendMove', position)
      .catch(err => console.error('Error sending move:', err));
  }

  public resetGame(): void {
    this.hubConnection.invoke('ResetGame')
      .catch(err => console.error('Error resetting game:', err));
  }
}
