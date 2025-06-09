import { Bool, OpenAPIRoute } from "chanfana";
import { z } from "zod";
import { type AppContext, State } from "../types";

export class StateCreateUpdate extends OpenAPIRoute {
	schema = {
		tags: [],
		summary: "Create or update the client's state",
		request: {
			body: {
				content: {
					"application/json": {
						schema: State,
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
									state: State,
								}),
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
		const taskToCreate = data.body;

		// Implement your own object insertion here

		// return the new task
		return {
			success: true,
			state: {
				uuid: taskToCreate.uuid,
				dailyTimeLimit: taskToCreate.dailyTimeLimit,
				remainingTime: taskToCreate.remainingTime,
				usedTime: taskToCreate.usedTime,
				remainingTimeDay: taskToCreate.remainingTimeDay,
				bedTime: taskToCreate.bedTime,
				wakeTime: taskToCreate.wakeTime,
				graceGiven: taskToCreate.graceGiven,
			},
		};
	}
}
