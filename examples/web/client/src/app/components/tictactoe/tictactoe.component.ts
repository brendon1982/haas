import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SignalRService } from '../../services/signalr.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-tictactoe',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tictactoe.component.html'
})
export class TicTacToeComponent implements OnInit, OnDestroy {
  public board: string[] = Array(9).fill(' ');
  public isPlayerTurn: boolean = true;
  public status: string = 'Your turn (X)';
  public aiLog: string[] = [];
  public connectionStatus: string = 'Connected';
  private subscription: Subscription = new Subscription();

  constructor(private signalRService: SignalRService) {}

  ngOnInit(): void {
    this.signalRService.startConnection();

    this.subscription.add(
      this.signalRService.messageReceived$.subscribe(data => {
        if (data.sourceType === 'tictactoe') {
          this.aiLog.unshift(data.message);
          if (this.aiLog.length > 5) this.aiLog.pop();
          this.isPlayerTurn = true;
          this.status = 'Your turn (X)';
        }
      })
    );

    this.subscription.add(
      this.signalRService.processingStarted$.subscribe(sourceType => {
        if (sourceType === 'tictactoe') {
          this.isPlayerTurn = false;
          this.status = 'AI is thinking...';
        }
      })
    );

    this.subscription.add(
      this.signalRService.boardUpdated$.subscribe(board => {
        this.board = board;
      })
    );

    this.subscription.add(
      this.signalRService.errorReceived$.subscribe(data => {
        if (data.sourceType === 'tictactoe') {
          this.status = `Error: ${data.error}`;
          this.isPlayerTurn = true;
        }
      })
    );

    this.subscription.add(
      this.signalRService.connectionState$.subscribe(state => {
        this.connectionStatus = state;
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
    if (this.isPlayerTurn && this.board[index] === ' ') {
      this.isPlayerTurn = false;
      this.status = 'AI is thinking...';
      this.signalRService.sendMove(index + 1);
    }
  }

  public reset(): void {
    this.board = Array(9).fill(' ');
    this.aiLog = [];
    this.isPlayerTurn = true;
    this.status = 'Your turn (X)';
    this.signalRService.resetGame();
  }
}
