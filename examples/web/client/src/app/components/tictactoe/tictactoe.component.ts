import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { TicTacToeSignalRService } from '../../services/tictactoe-signalr.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-tictactoe',
  imports: [],
  templateUrl: './tictactoe.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TicTacToeComponent implements OnInit, OnDestroy {
  public signalRService = inject(TicTacToeSignalRService);
  public board = signal<string[]>(Array(9).fill(' '));
  public isPlayerTurn = signal<boolean>(true);
  public status = signal<string>('Your turn (X)');
  public aiLog = signal<string[]>([]);
  public winner = signal<'X' | 'O' | null>(null);
  public isDraw = signal(false);
  public gameOver = signal(false);
  public displayStatus = computed(() => {
    if (this.winner() === 'X') {
      return 'You win!';
    }

    if (this.winner() === 'O') {
      return 'AI wins!';
    }

    if (this.isDraw()) {
      return "It's a draw!";
    }

    return this.status();
  });
  
  public connectionStatus = computed(() => {
    return this.signalRService.connectionState();
  });

  private subscription: Subscription = new Subscription();

  ngOnInit(): void {
    this.signalRService.startConnection();

    this.subscription.add(
      this.signalRService.messageReceived$.subscribe(data => {
        if (data.sourceType === 'tictactoe') {
          this.aiLog.update(log => {
            const newLog = [data.message, ...log];
            return newLog.slice(0, 5);
          });
          if (!this.gameOver()) {
            this.isPlayerTurn.set(true);
            this.status.set('Your turn (X)');
          }
        }
      })
    );

    this.subscription.add(
      this.signalRService.processingStarted$.subscribe(data => {
        if (data.sourceType === 'tictactoe' && !this.gameOver()) {
          this.isPlayerTurn.set(false);
          this.status.set('AI is thinking...');
        }
      })
    );

    this.subscription.add(
      this.signalRService.boardUpdated$.subscribe(state => {
        this.board.set(state.board);
        this.winner.set(state.winner);
        this.isDraw.set(state.isDraw);
        this.gameOver.set(state.isGameOver);
        if (state.isGameOver) {
          this.isPlayerTurn.set(false);
        }
      })
    );

    this.subscription.add(
      this.signalRService.errorReceived$.subscribe(data => {
        if (data.sourceType === 'tictactoe') {
          this.status.set(`Error: ${data.error}`);
          this.isPlayerTurn.set(true);
        }
      })
    );

    this.subscription.add(
      this.signalRService.reconnected$.subscribe(() => {
        // Request board refresh on reconnection
        this.signalRService.resetGame();
      })
    );
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  public makeMove(index: number): void {
    if (!this.gameOver() && this.isPlayerTurn() && this.board()[index] === ' ') {
      this.isPlayerTurn.set(false);
      this.status.set('AI is thinking...');
      this.signalRService.sendMove(index + 1);
    }
  }

  public reset(): void {
    this.board.set(Array(9).fill(' '));
    this.winner.set(null);
    this.isDraw.set(false);
    this.gameOver.set(false);
    this.aiLog.set([]);
    this.isPlayerTurn.set(true);
    this.status.set('Your turn (X)');
    this.signalRService.resetGame();
  }
}
