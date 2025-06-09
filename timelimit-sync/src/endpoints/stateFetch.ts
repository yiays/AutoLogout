import { Bool, OpenAPIRoute, Str } from "chanfana";
import { z } from "zod";
import { type AppContext, State } from "../types";

export class StateFetch extends OpenAPIRoute {
	schema = {
		tags: [],
		summary: "Get a client's state by uuid",
		request: {
			params: z.object({
				uuid: Str({ description: "Client uuid" }),
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
									state: State,
								}),
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
		const { uuid } = data.params;

		// Implement your own object fetch here

		const exists = true;

		// @ts-ignore: check if the object exists
		if (exists === false) {
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

		return {
			success: true,
			state: {}, // Replace with actual state object
		};
	}
}
