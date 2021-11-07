import React, { useState } from "react";

import { useForm } from "react-hook-form";
import * as BS from "react-bootstrap";
import moment from "moment";
import "moment-timezone";

import API_URL from "../../../Constants/constants.js";

const EditSystem = ({
	name,
	tag,
	timezone,
	avatar,
	banner,
	editDesc,
	setEditMode,
	setErrorAlert,
	user,
	setUser,
	setErrorMessage
}) => {
	const [invalidTimezone, setInvalidTimezone] = useState(false);

	const { register: registerEdit, handleSubmit: handleSubmitEdit } = useForm();

	const submitEdit = (data) => {
	if (data.tz) {
		if (!moment.tz.zone(data.tz)) {
		setInvalidTimezone(true);
		return;
		}
	}
	fetch(`${API_URL}s`, {
		method: "PATCH",
		body: JSON.stringify(data),
		headers: {
		"Content-Type": "application/json",
		Authorization: localStorage.getItem("token"),
		},
	})
		.then((res) => {
		if (!res.ok)
			throw new Error('HTTP Status ' + res.status)
		return res.json();
		})
		.then(() => {
		setUser((prevState) => {
			return { ...prevState, ...data };
		});
		localStorage.setItem("user", JSON.stringify(user));
		setEditMode(false);
		})
		.catch((error) => {
		console.log(error);
		setErrorMessage(error.message);
		if (error.message === 'HTTP Status 401') {
			setErrorMessage("Your token is invalid, please log out and enter a new token.")
		};
		if (error.message === 'HTTP Status 500') {
			setErrorMessage("500: Internal server error.")
		}
		setErrorAlert(true);
		});
	};

	return (
	<BS.Form onSubmit={handleSubmitEdit(submitEdit)}>
		<BS.Form.Text className="mb-4">
		<b>Note:</b> if you refresh the page, the old data might show up again,
		this is due to the bot caching data.
		<br />
		Try editing a member to clear the cache, or wait a few minutes before
		refreshing.
		</BS.Form.Text>
		<BS.Form.Row>
		<BS.Col className="mb-lg-2" xs={12} lg={3}>
			<BS.Form.Label>Name:</BS.Form.Label>
			<BS.Form.Control
			name="name"
			{...registerEdit("name")}
			defaultValue={name}
			/>
		</BS.Col>
		<BS.Col className="mb-lg-2" xs={12} lg={3}>
			<BS.Form.Label>Tag:</BS.Form.Label>
			<BS.Form.Control
			name="tag"
			{...registerEdit("tag")}
			defaultValue={tag}
			/>
		</BS.Col>
		<BS.Col className="mb-lg-2" xs={12} lg={3}>
			<BS.Form.Label>Timezone:</BS.Form.Label>
			<BS.Form.Control
			name="tz"
			{...registerEdit("tz")}
			defaultValue={timezone}
			required
			/>
			{invalidTimezone ? (
			<BS.Form.Text>
				Please enter a valid
				<a
				href="https://xske.github.io/tz/"
				rel="noreferrer"
				target="_blank"
				>
				timezone
				</a>
			</BS.Form.Text>
			) : (
			""
			)}
		</BS.Col>
		<BS.Col className="mb-lg-2" xs={12} lg={3}>
			<BS.Form.Label>Avatar url:</BS.Form.Label>
			<BS.Form.Control
			type="url"
			name="avatar_url"
			{...registerEdit("avatar_url")}
			defaultValue={avatar}
			/>
		</BS.Col>
		<BS.Col className="mb-lg-2" xs={12} lg={3}>
			<BS.Form.Label>Banner url:</BS.Form.Label>
			<BS.Form.Control
			type="url"
			name="banner"
			{...registerEdit("banner")}
			defaultValue={banner}
			/>
		</BS.Col>
		</BS.Form.Row>
		<BS.Form.Group className="mt-3">
		<BS.Form.Label>Description:</BS.Form.Label>
		<BS.Form.Control
			maxLength="1000"
			as="textarea"
			name="description"
			{...registerEdit("description")}
			defaultValue={editDesc}
		/>
		</BS.Form.Group>
		<BS.Button variant="light" onClick={() => setEditMode(false)}>
		Cancel
		</BS.Button>{" "}
		<BS.Button variant="primary" type="submit">
		Submit
		</BS.Button>
	</BS.Form>
	);
};

export default EditSystem;
