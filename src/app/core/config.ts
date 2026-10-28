// Local development talks to the local API; any other host uses the deployed API.
const isLocal = ['localhost', '127.0.0.1'].includes(location.hostname);

// After deploying the API (see DEPLOY.md), put its public URL here, e.g. https://chat-api-xyz.azurewebsites.net
const PROD_API_ORIGIN = 'https://YOUR-API-NAME.azurewebsites.net';

export const API_ORIGIN = isLocal ? 'https://localhost:7001' : PROD_API_ORIGIN; // local port = the one `dotnet run` prints
export const API_URL = `${API_ORIGIN}/api`;
export const HUB_URL = `${API_ORIGIN}/hubs/chat`;
