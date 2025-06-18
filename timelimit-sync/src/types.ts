import { Bool, DateOnly, DateTime, Int, Str } from "chanfana";
import type { Context } from "hono";
import { z } from "zod";

export type AppContext = Context<{ Bindings: Env }>;

// Common core values all states should have
export const BaseState = z.object({
	hashedPassword: Str({ example: "$2a$11$..." })
		.regex(/^\$2[aby]\$\d{2}\$[./A-Za-z0-9]{53}$/, { message: "Invalid bcrypt hash" }),
	dailyTimeLimit: Int({ example: 7200}).gte(-1).lt(86400),
	todayTimeLimit: Int({ example: 7200 }).gte(-1).lt(86400),
	usedTime: Int({ example: 0 }).gte(-1).lt(86400).optional(),
	usageDate: Str({ example: "2024-01-15" })
		.regex(/^\d{4}\-\d{2}\-\d{2}$/, { message: "Invalid date format, expected YYYY-MM-DD" }),
	bedtime: Str({ example: "22:00:00" })
		.regex(/^(0[0-9]|1[0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]$/, { message: "Invalid time format, expected HH:MM" }),
	waketime: Str({ example: "22:00:00" })
		.regex(/^(0[0-9]|1[0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]$/, { message: "Invalid time format, expected HH:MM" }),
	graceGiven: Bool({ example: false }),
	syncAuthor: Str().uuid(),
})

// States when syncing with the client
export const SyncState = z.object({
	...BaseState.shape,
	hashedPassword: BaseState.shape.hashedPassword.optional(),
	syncAuthor: BaseState.shape.syncAuthor.optional().nullable(),
});

// States including security information that is kept on the server
export const SecureState = z.object({
	...BaseState.shape,
	authKeys: z.array(Str().uuid()).default([]),
});