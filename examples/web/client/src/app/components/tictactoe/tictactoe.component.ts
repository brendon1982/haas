import { Component, OnInit, OnDestroy, signal, computed, ChangeDetectionStrategy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SignalRService } from '../../services/signalr.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-tictactoe',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tictactoe.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TicTacToeComponent implements OnInit, OnDestroy {
  public signalRService = inject(SignalRService);
  public board = signal<string[]>(Array(9).fill(' '));
  public isPlayerTurn = signal<boolean>(true);
  public status = signal<string>('Your turn (X)');
  public aiLog = signal<string[]>([]);
  
  public connectionStatus = computed(() => {
    return this.signalRService.connectionState();
  });

  private subscription: Subscription = new Subscription();

  constructor() {}

  ngOnInit(): void {
    this.signalRService.startConnection();

    this.subscription.add(
      this.signalRService.messageReceived$.subscribe(data => {
        if (data.sourceType === 'tictactoe') {
          this.aiLog.update(log => {
            const newLog = [data.message, ...log];
            return newLog.slice(0, 5);
          });
          this.isPlayerTurn.set(true);
          this.status.set('Your turn (X)');
        }
      })
    );

    this.subscription.add(
      this.signalRService.processingStarted$.subscribe(data => {
        if (data.sourceType === 'tictactoe') {
          this.isPlayerTurn.set(false);
          this.status.set('AI is thinking...');
        }
      })
    );

    this.subscription.add(
      this.signalRService.boardUpdated$.subscribe(board => {
        this.board.set(board);
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
    if (this.isPlayerTurn() && this.board()[index] === ' ') {
      this.isPlayerTurn.set(false);
      this.status.set('AI is thinking...');
      this.signalRService.sendMove(index + 1);
    }
  }

  public reset(): void {
    this.board.set(Array(9).fill(' '));
    this.aiLog.set([]);
    this.isPlayerTurn.set(true);
    this.status.set('Your turn (X)');
    this.signalRService.resetGame();
  }
}
