import React, { useState, useEffect } from "react";
import * as BS from "react-bootstrap";
import { useRouteMatch } from "react-router-dom";
import autosize from "autosize";
import "moment-timezone";
import Popup from "reactjs-popup";
import twemoji from 'twemoji';

import history from "../../History.js";
import defaultAvatar from "../../default_discord_avatar.png";
import { FaAddressCard } from "react-icons/fa";
import EditSystem from "./Edit/EditSystem.js";
import EditSystemPrivacy from "./Edit/EditSystemPrivacy.js";

export default function System() {
	// match the url, if there's a member ID there, don't render this component at all
	const match = useRouteMatch("/dash/:memberID");

	// get the user from the localstorage
	const [user, setUser] = useState(JSON.parse(localStorage.getItem("user")));

	// bunch of useState stuff, used in the useEffect() hook below
	const [name, setName] = useState("");
	const [tag, setTag] = useState("");
	const [timezone, setTimezone] = useState("");
	const [avatar, setAvatar] = useState("");
	const [banner, setBanner] = useState("");
	const [desc, setDesc] = useState("");
	const [editDesc, setEditDesc] = useState("");

	// more useState, this time to actually handle state
	// TODO: name them something more intuitive maybe?
	const [editMode, setEditMode] = useState(false);
	const [privacyEdit, setPrivacyEdit] = useState(false);
	const [privacyView, setPrivacyView] = useState(false);

	const [errorAlert, setErrorAlert] = useState(false);
	const [ errorMessage, setErrorMessage ] = useState("");

	// this useEffect does a couple of things after the user is gotten from localstorage
	useEffect(() => {
		// first require the discord markdown parser
		const { toHTML } = require("../../Functions/discord-parser.js");

		// check if there's a name object in user, if it's null, set name to a blank string, otherwise set name to user.name
		if (user.name) {
			setName(user.name);
		} else setName("");

		// same as above, but with the user tag instead
		if (user.tag) {
			setTag(user.tag);
		} else setTag("");

		// same as above but with timezone
		if (user.tz) {
			setTimezone(user.tz);
		} else setTimezone("");

		// also trims the avatar url so that 1. pngs won't be converted to jpegs and 2. won't be resized to 256x256
		if (user.avatar_url) {
			var avatarsmall = user.avatar_url.replace("&format=jpeg", "");
			setAvatar(avatarsmall.replace("?width=256&height=256", ""));
		} else setAvatar("");

		if (user.banner) {
			setBanner(user.banner);
		} else setBanner("");

		// same as above, but with descriptions
		// two description variables! one is just the plain description, the other is parsed and converted to html
		if (user.description) {
			setDesc(toHTML(user.description));
			setEditDesc(user.description);
		} else {
			setDesc("(no description)");
			setEditDesc("");
		}
	}, [user.description, user.tag, user.avatar_url, user.tz, user.name, user.banner]);

	// this just resizes the textarea when filled with larger amounts of text
	useEffect(() => {
		autosize(document.querySelector("textarea"));
	});

	if (match) return null;

	return (
		<>
		{ user.banner && !localStorage.getItem("hidebanners") ? <div className="banner" style={{backgroundImage: `url(${user.banner})`}} alt=""/> : ""}
		<BS.Card className="mb-3 mt-3 w-100">
			<BS.Card.Header className="d-flex align-items-center justify-content-between">
				<BS.Card.Title className="float-left">
					<FaAddressCard className="mr-4 float-left" /> {name} ({user.id})
				</BS.Card.Title>
				{user.avatar_url ? (
					<Popup
						trigger={
							<BS.Image
								src={`${user.avatar_url}`}
								style={{ width: 50, height: 50 }}
								tabIndex="0"
								className="float-right"
								roundedCircle
							/>
						}
						className="avatar"
						modal
					>
						{(close) => (
							<div className="text-center w-100 m-0" onClick={() => close()}>
								<div className="m-auto" style={{maxWidth: '640px'}}>
										<BS.Image src={`${avatar}`} style={{'maxWidth': '100%', height: 'auto'}} thumbnail />
								</div>
							</div>
						)}
					</Popup>
				) : (
					<BS.Image
						src={defaultAvatar}
						style={{ width: 50, height: 50 }}
						className="float-right"
						roundedCircle
					/>
				)}
			</BS.Card.Header>
			<BS.Card.Body>
				{errorAlert ? (
					<BS.Alert variant="danger">
						{errorMessage}
					</BS.Alert>
				) : (
					""
				)}
				{editMode ? (
					<EditSystem
						editDesc={editDesc}
						name={name}
						tag={tag}
						timezone={timezone}
						avatar={avatar}
						banner={banner}
						setErrorAlert={setErrorAlert}
						user={user}
						setUser={setUser}
						setEditMode={setEditMode}
						setErrorMessage={setErrorMessage}
					/>
				) : (
					<>
						<BS.Row>
							<BS.Col className="mb-lg-3" xs={12} lg={3}>
								<b>ID:</b> {user.id}
							</BS.Col>
							<BS.Col className="mb-lg-3" xs={12} lg={3}>
								<b>Tag:</b> {tag}
							</BS.Col>
							<BS.Col className="mb-lg-3" xs={12} lg={3}>
								<b>Timezone:</b> {timezone}
							</BS.Col>
							{privacyView ? (
								""
							) : (
								<>
								<BS.Col className="mb-lg-3" xs={12} lg={3}>
									<b>Privacy:</b>{" "}
									<BS.Button
										variant="light"
										size="sm"
										onClick={() => setPrivacyView(true)}
									>
										View
									</BS.Button>
								</BS.Col>
								{user.banner ? 
									<BS.Col className="mb-lg-3" xs={12} lg={3}>
									<b>Banner:</b>{" "}
									<Popup
										trigger={
											<BS.Button
										variant="light"
										size="sm"
									>
										View
									</BS.Button>
										}
										className="banner"
										modal
									>
										{(close) => (
											<div className="text-center w-100" onClick={() => close()}>
												<div className="m-auto" style={{maxWidth: '100%'}}>
														<BS.Image src={`${banner}`} style={{maxWidth: 'auto', maxHeight: '640px'}} thumbnail />
												</div>
											</div>
										)}
									</Popup>
									</BS.Col>
								 : "" }
								 </> 
								 )}
						</BS.Row>
						{privacyEdit ? (
							<EditSystemPrivacy
								setErrorAlert={setErrorAlert}
								setUser={setUser}
								user={user}
								setPrivacyEdit={setPrivacyEdit}
								setErrorMessage={setErrorMessage}
							/>
						) : privacyView ? (
							<>
								<hr />
								<h5>Viewing privacy settings</h5>
								<BS.Row>
									<BS.Col className="mb-lg-3" xs={12} lg={3}>
										<b>Description:</b> {user.description_privacy}
									</BS.Col>
									<BS.Col className="mb-lg-3" xs={12} lg={3}>
										<b>Member list: </b>
										{user.member_list_privacy}
									</BS.Col>
									<BS.Col className="mb-lg-3" xs={12} lg={3}>
										<b>Front:</b> {user.front_privacy}
									</BS.Col>
									<BS.Col className="mb-lg-3" xs={12} lg={3}>
										<b>Front history:</b> {user.front_history_privacy}
									</BS.Col>
								</BS.Row>
								<BS.Button
									variant="light"
									onClick={() => setPrivacyView(false)}
								>
									Exit
								</BS.Button>{" "}
								<BS.Button
									variant="primary"
									onClick={() => setPrivacyEdit(true)}
								>
									Edit
								</BS.Button>
								<hr />
							</>
						) : (
							""
						)}
						<p>
							<b>Description:</b>
						</p>
						{ localStorage.getItem("twemoji") ? <p dangerouslySetInnerHTML={{__html: twemoji.parse(desc)}}></p> : <p dangerouslySetInnerHTML={{__html: desc}}></p>}
						{ !user.banner || !localStorage.getItem("bottombanners") ? "" : 
							<BS.Image rounded className="mb-2" style={{width: '100%', maxHeight: '15rem', objectFit: 'cover'}} src={banner}/>
						}
						{privacyEdit ? (
							""
						) : privacyView ? (
							""
						) : (
							<>
								<BS.Button variant="light" onClick={() => setEditMode(true)}>
									Edit
								</BS.Button>
								<BS.Button
									variant="primary"
									className="float-right"
									onClick={() => history.push(`/profile/${user.id}`)}
								>
									Profile
								</BS.Button>
							</>
						)}
					</>
				)}
			</BS.Card.Body>
		</BS.Card>
		</>
	);
}
