import { Bool, OpenAPIRoute } from "chanfana";
import { z } from "zod";
import { type AppContext, SecureState, SyncState } from "../types";

export class StateSync extends OpenAPIRoute {
	schema = {
		tags: [],
		summary: "Create or update the client's state",
		request: {
			body: {
				content: {
					"application/json": {
						schema: SyncState,
					},
				},
			},
		},
		responses: {
			"200": {
				description: "Returns the created state",
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
		},
	};

	async handle(c: AppContext) {
		// Get validated data
		const data = await this.getValidatedData<typeof this.schema>();

		// Retrieve the validated request body
		const { authKey, ...newState} = data.body;

		const stateType = z.object(SyncState.shape);
		const secureStateType = z.object(SecureState.shape);
		
		// Retrieve existing state
		let rawState: string | null = await c.env.timelimit.get(newState.uuid);
		console.log("Raw state:", rawState);
		if (rawState) {
			// State exists
			const parsedState = secureStateType.parse(JSON.parse(rawState));

			// Check if the client is authenticated
			if (!(authKey && parsedState.authKeys.includes(authKey))) {
				return c.json({
					series: {
						success: false,
						error: "Unauthorized",
					},
				}, 401);
			}

			// Update existing state
			const state = {
				...parsedState,
				...newState,
			};
			await c.env.timelimit.put(state.uuid, JSON.stringify(state));

			// return the updated State
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
			// Create new state
			const {authKey: _, ...parsedState} = stateType.parse(newState);
			const newAuthKey = crypto.randomUUID();
			const state = {
				...parsedState,
				authKeys: [newAuthKey],
			};
			await c.env.timelimit.put(state.uuid, JSON.stringify(state));

			// return the created State
			return {
				success: true,
				state: {
					authKey: newAuthKey,
					dailyTimeLimit: state.dailyTimeLimit,
					remainingTime: state.remainingTime,
					usedTime: state.usedTime,
					remainingTimeDay: state.remainingTimeDay,
					bedtime: state.bedtime,
					waketime: state.waketime,
					graceGiven: state.graceGiven,
				},
			};
		}
	}
}
