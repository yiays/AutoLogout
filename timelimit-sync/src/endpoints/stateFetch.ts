import { Bool, OpenAPIRoute, Str } from "chanfana";
import { z } from "zod";
import { type AppContext, SyncState, SecureState } from "../types";

export class StateFetch extends OpenAPIRoute {
	schema = {
		tags: [],
		summary: "Get a client's state by uuid",
		request: {
			params: z.object({
				uuid: Str({ description: "Client uuid" }).uuid(),
				authKey: Str({ description: "Client auth key" }).uuid(),
			}),
		},
		responses: {
			"200": {
				description: "Returns state for uuid if found",
				content: {
					"application/json": {
						schema: z.object({
							series: z.object({
								success: Bool(),
								result: z.object({
									state: SyncState,
								}),
							}),
						}),
					},
				},
			},
			"401": {
				description: "Unauthorized",
				content: {
					"application/json": {
						schema: z.object({
							series: z.object({
								success: Bool(),
								error: z.string(),
							}),
						}),
					},
				},
			},
			"404": {
				description: "State not found",
				content: {
					"application/json": {
						schema: z.object({
							series: z.object({
								success: Bool(),
								error: Str(),
							}),
						}),
					},
				},
			},
		},
	};

	async handle(c: AppContext) {
		// Get validated data
		const data = await this.getValidatedData<typeof this.schema>();

		// Retrieve the validated slug
		const secureStateType = z.object(SecureState.shape);

		const { uuid, authKey } = data.params;
		let rawState: string | null = await c.env.timelimit.get(uuid);
		if (rawState) {
			// State exists
			const state = secureStateType.parse(JSON.parse(rawState));

			// Check if the client is authenticated
			if (!(authKey && state.authKeys.includes(authKey))) {
				return c.json({
					series: {
						success: false,
						error: "Unauthorized",
					},
				}, 401);
			}

			// Return the state
			return {
				success: true,
				state: {
					dailyTimeLimit: state.dailyTimeLimit,
					remainingTime: state.remainingTime,
					usedTime: state.usedTime,
					remainingTimeDay: state.remainingTimeDay,
					bedtime: state.bedtime,
					waketime: state.waketime,
					graceGiven: state.graceGiven,
				},
			};
		} else {
			return Response.json(
				{
					success: false,
					error: "Object not found",
				},
				{
					status: 404,
				},
			);
		}
	}
}
