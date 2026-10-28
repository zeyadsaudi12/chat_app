import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from './auth.service';
import { HUB_URL } from './config';
import { ChatConversation, ChatMessage, ReadReceipt, TypingEvent } from '../chat/chat.models';

@Injectable({ providedIn: 'root' })
export class ChatRealtimeService {
  private auth = inject(AuthService);
  private connection?: HubConnection;

  readonly messages$ = new Subject<ChatMessage>();
  readonly typing$ = new Subject<TypingEvent>();
  readonly receipts$ = new Subject<ReadReceipt>();
  readonly conversationAdded$ = new Subject<ChatConversation>();
  /** Emits after an automatic reconnect so the UI can resync history it may have missed. */
  readonly reconnected$ = new Subject<void>();

  async connect(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) return;

    this.connection = new HubConnectionBuilder()
      .withUrl(HUB_URL, { accessTokenFactory: () => this.auth.token() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('MessageReceived', (m: ChatMessage) => this.messages$.next(m));
    this.connection.on('TypingChanged', (e: TypingEvent) => this.typing$.next(e));
    this.connection.on('ReadReceipt', (r: ReadReceipt) => this.receipts$.next(r));
    this.connection.on('ConversationAdded', (c: ChatConversation) => this.conversationAdded$.next(c));
    // The server re-adds the connection to all of the user's groups in OnConnectedAsync.
    this.connection.onreconnected(() => this.reconnected$.next());

    await this.connection.start();
  }

  private get ready(): HubConnection {
    if (!this.connection || this.connection.state !== HubConnectionState.Connected)
      throw new Error('Chat is not connected.');
    return this.connection;
  }

  joinConversation(id: string) { return this.ready.invoke('JoinConversation', id); }

  sendMessage(conversationId: string, content: string, attachmentIds: string[] = []) {
    return this.ready.invoke('SendMessage', { conversationId, content, attachmentIds });
  }

  async setTyping(conversationId: string, isTyping: boolean) {
    if (this.connection?.state === HubConnectionState.Connected)
      await this.connection.invoke('SendTyping', conversationId, isTyping);
  }

  async markRead(conversationId: string) {
    if (this.connection?.state === HubConnectionState.Connected)
      await this.connection.invoke('MarkRead', conversationId);
  }

  async disconnect() {
    await this.connection?.stop();
    this.connection = undefined;
  }
}
