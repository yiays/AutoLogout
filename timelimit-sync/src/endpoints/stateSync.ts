import { Bool, OpenAPIRoute, Str } from "chanfana";
import { z, } from "zod";
import { type AppContext, SecureState, SyncState } from "../types";

export class StateSync extends OpenAPIRoute {
	schema = {
		tags: [],
		summary: "Create or update the client's state",
		request: {
			params: z.object({
				uuid: Str({ description: "Target client UUID" }).uuid(),
			}),
			query: z.object({
				parentMode: z.coerce.boolean({
					description: "Overrides values even if they are different from what you expected."
				}).optional().default(false),
			}),
			headers: z.object({
				Authorization: Str({ description: "Your auth key as a bearer token" }).optional(),
			}),
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
				description: "Returns accepted if your changes were accepted, and any fields that might be different from your submission",
				content: {
					"application/json": {
						schema: z.object({
							series: z.object({
								accepted: Bool(),
								diff: SyncState.partial().optional(),
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
								accepted: Bool(),
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

		// Retrieve request parameters
		const { uuid } = data.params;
		const { parentMode } = data.query;
		const authKey = data.headers.Authorization?.startsWith("Bearer ")
			? data.headers.Authorization.split(" ")[1]
			: data.headers.Authorization;

		// Retrieve the validated request body
		const { uuid:_, syncAuthor, ...newState} = data.body;

		const stateType = z.object(SyncState.shape);
		const secureStateType = z.object(SecureState.shape);
		
		// Retrieve existing state
		let rawState: string | null = await c.env.timelimit.get(uuid);
		if (rawState) {
			// State exists
			const oldState = secureStateType.parse(JSON.parse(rawState));

			// Check if the client is authenticated
			if (authKey && oldState.authKeys.includes(authKey)) {
				if(parentMode || [authKey, syncAuthor].includes(oldState.syncAuthor)) {
					// Client already knows or doesn't need to know about old state
					// Update existing state
					const state = {
						...oldState,
						...newState,
						syncAuthor: authKey,
					}
					await c.env.timelimit.put(state.uuid, JSON.stringify(state));

					// Inform client the changes were accepted
					return {
						accepted: true,
					}
				} else {
					// Client is not yet aware of changes made by another client
					return {
						accepted: false,
						diff: {
							dailyTimeLimit: oldState.dailyTimeLimit,
							remainingTime: oldState.remainingTime,
							usedTime: oldState.usedTime,
							remainingTimeDay: oldState.remainingTimeDay,
							bedtime: oldState.bedtime,
							waketime: oldState.waketime,
							graceGiven: oldState.graceGiven,
							syncAuthor: oldState.syncAuthor,
						}
					}
				}
			} else {
				return c.json({
					series: {
						accepted: false,
						error: "Unauthorized",
					},
				}, 401);
			}
		} else {
			// Create new state
			const {authKey: _, ...parsedState} = stateType.parse(newState);
			const newAuthKey = crypto.randomUUID();
			const state = {
				...parsedState,
				authKeys: [newAuthKey],
				syncAuthor: newAuthKey,
			}
			await c.env.timelimit.put(state.uuid, JSON.stringify(state));

			// return the created State
			return {
				accepted: true,
				diff: {
					authKey: newAuthKey,
				},
			}
		}
	}
}
