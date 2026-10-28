import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from './config';
import { ChatAttachment, ChatConversation, ChatMessage, UserSummary } from '../chat/chat.models';

@Injectable({ providedIn: 'root' })
export class ChatApiService {
  private http = inject(HttpClient);

  getConversations(): Observable<ChatConversation[]> {
    return this.http.get<ChatConversation[]>(`${API_URL}/conversations`);
  }

  getMessages(conversationId: string, before?: string): Observable<ChatMessage[]> {
    let params = new HttpParams().set('take', 50);
    if (before) params = params.set('before', before);
    return this.http.get<ChatMessage[]>(`${API_URL}/conversations/${conversationId}/messages`, { params });
  }

  searchUsers(q: string): Observable<UserSummary[]> {
    return this.http.get<UserSummary[]>(`${API_URL}/users`, { params: new HttpParams().set('q', q) });
  }

  createPrivate(userId: string): Observable<ChatConversation> {
    return this.http.post<ChatConversation>(`${API_URL}/conversations/private`, { userId });
  }

  createGroup(title: string, memberIds: string[]): Observable<ChatConversation> {
    return this.http.post<ChatConversation>(`${API_URL}/conversations/group`, { title, memberIds });
  }

  upload(file: File): Observable<ChatAttachment> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ChatAttachment>(`${API_URL}/uploads`, form);
  }

  download(id: string): Observable<Blob> {
    return this.http.get(`${API_URL}/uploads/${id}`, { responseType: 'blob' });
  }
}
