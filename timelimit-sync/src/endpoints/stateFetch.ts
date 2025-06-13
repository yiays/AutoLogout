import { Bool, OpenAPIRoute, Str } from "chanfana";
import { z } from "zod";
import { type AppContext, SyncState, SecureState } from "../types";

export class StateFetch extends OpenAPIRoute {
	schema = {
		tags: [],
		summary: "Get a client's state by uuid",
		request: {
			params: z.object({
				uuid: Str({ description: "Target client UUID" }).uuid(),
			}),
			query: z.object({
				authKey: Str({ description: "Your auth key" }).uuid(),
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
								error: Str(),
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

		// Create type for secure state
		const secureStateType = z.object(SecureState.shape);

		// Handle request parameters
		const { uuid } = data.params;
		const { authKey } = data.query;

		// Retrieve state if it exists
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
			return c.json({
				series: {
					success: false,
					error: "Client not found",
				}
			}, 404);
		}
	}
}
