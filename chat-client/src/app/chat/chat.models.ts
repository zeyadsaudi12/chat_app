export type ConversationType = 'private' | 'group';

export interface ChatConversation {
  id: string;
  title: string;
  type: ConversationType;
  unreadCount: number;
  lastMessage?: string | null;
  lastAt?: string | null;
  memberCount: number;
}

export interface ChatAttachment {
  id: string;
  fileName: string;
  contentType: string;
  size: number;
}

export interface ChatMessage {
  id: string;
  conversationId: string;
  senderId: string;
  senderName: string;
  content: string;
  sentAt: string;
  readBy: string[];
  attachments: ChatAttachment[];
}

export interface TypingEvent {
  conversationId: string;
  userId: string;
  userName: string;
  isTyping: boolean;
}

export interface ReadReceipt {
  conversationId: string;
  userId: string;
  messageIds: string[];
  readAt: string;
}

export interface UserSummary {
  id: string;
  userName: string;
  displayName: string;
}
