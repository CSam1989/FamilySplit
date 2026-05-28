// Retired: the handoff cookie + /auth/handoff endpoint were replaced by the
// refresh-token flow. The OAuth callback now sets an HttpOnly refresh cookie,
// and the client calls IAuthApi.RefreshAsync() from /auth/return to obtain
// the first JWT. This file is intentionally empty so the change shows up
// cleanly in git history; it can be deleted in the next cleanup commit.
