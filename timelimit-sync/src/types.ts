import { Bool, DateOnly, DateTime, Int, Str } from "chanfana";
import type { Context } from "hono";
import { z } from "zod";

export type AppContext = Context<{ Bindings: Env }>;

export const State = z.object({
	uuid: Str(),
	hashedPassword: Str({ example: "$2a$11$..." }),
	dailyTimeLimit: Int({ example: 7200}),
	remainingTime: Int({ example: 7200 }),
	usedTime: Int({ example: 0 }),
	remainingTimeDay: DateOnly({ example: "01/10/2024" }),
	bedTime: Str({ example: "22:00" }),
	wakeTime: Str({ example: "22:00" }),
	graceGiven: Bool({ example: false }),
});
