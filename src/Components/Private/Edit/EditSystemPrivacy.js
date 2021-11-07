import React from "react";

import { useForm } from "react-hook-form";
import * as BS from "react-bootstrap";

import API_URL from "../../../Constants/constants.js";

const EditSystemPrivacy = ({
	setErrorAlert,
	setUser,
	user,
	setPrivacyEdit,
	setErrorMessage
}) => {
	const { register: registerPrivacy, handleSubmit: handleSubmitPrivacy } =
		useForm();

	// submit privacy stuffs
	const submitPrivacy = (data) => {
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
			setPrivacyEdit(false);
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
		<BS.Form onSubmit={handleSubmitPrivacy(submitPrivacy)}>
			<hr />
			<h5>Editing privacy settings</h5>
			<BS.Form.Row className="mb-3 mb-lg-0">
				<BS.Col className="mb-lg-2" xs={12} lg={3}>
					<BS.Form.Label>Description:</BS.Form.Label>
					<BS.Form.Control
						name="description_privacy"
						defaultValue={user.description_privacy}
						as="select"
						{...registerPrivacy("description_privacy")}
					>
						<option>public</option>
						<option>private</option>
					</BS.Form.Control>
				</BS.Col>
				<BS.Col className="mb-lg-2" xs={12} lg={3}>
					<BS.Form.Label>Member list:</BS.Form.Label>
					<BS.Form.Control
						name="member_list_privacy"
						defaultValue={user.member_list_privacy}
						as="select"
						{...registerPrivacy("member_list_privacy")}
					>
						<option>public</option>
						<option>private</option>
					</BS.Form.Control>
				</BS.Col>
				<BS.Col className="mb-lg-2" xs={12} lg={3}>
					<BS.Form.Label>Front:</BS.Form.Label>
					<BS.Form.Control
						name="front_privacy"
						as="select"
						defaultValue={user.front_privacy}
						{...registerPrivacy("front_privacy")}
					>
						<option>public</option>
						<option>private</option>
					</BS.Form.Control>
				</BS.Col>
				<BS.Col className="mb-lg-2" xs={12} lg={3}>
					<BS.Form.Label>Front history:</BS.Form.Label>
					<BS.Form.Control
						name="front_history_privacy"
						defaultValue={user.front_history_privacy}
						as="select"
						{...registerPrivacy("front_history_privacy")}
					>
						<option>public</option>
						<option>private</option>
					</BS.Form.Control>
				</BS.Col>
			</BS.Form.Row>
			<BS.Button variant="light" onClick={() => setPrivacyEdit(false)}>
				Cancel
			</BS.Button>{" "}
			<BS.Button variant="primary" type="submit">
				Submit
			</BS.Button>
			<hr />
		</BS.Form>
	);
};

export default EditSystemPrivacy;
