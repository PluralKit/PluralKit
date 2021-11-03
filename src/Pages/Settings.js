import React from "react";

import * as BS from "react-bootstrap";
import { FaCog } from "react-icons/fa";
import Toggle from "react-toggle";

const Settings = ({ forceUpdate }) => {

// this all should be pretty self-explanatory, might add comments later tho
	return (
		<BS.Card>
			<BS.Card.Header className="d-flex align-items-center justify-content-between">
				<BS.Card.Title>
					<FaCog className="mr-3" />
					Settings
				</BS.Card.Title>
			</BS.Card.Header>
			<BS.Card.Body>
				<p>
					Change how you view and use pk-webs here, changes will be saved after
					refreshing. You will have to apply them again in different browsers
					and on different devices.
				</p>
				<hr />
				<BS.Row>
					<BS.Col
						xs={12}
						lg={4}
						className="mx-1 mb-4 d-flex align-items-center row"
					>
						{localStorage.getItem("opendyslexic") ? (
							<Toggle
								className="mr-2"
								defaultChecked={true}
								icons={false}
								onChange={() => {
									localStorage.removeItem("opendyslexic");
									forceUpdate();
								}}
							/>
						) : (
							<Toggle
								className="mr-2"
								defaultChecked={false}
								icons={false}
								onChange={() => {
									localStorage.setItem("opendyslexic", "true");
									forceUpdate();
								}}
							/>
						)}
						Use opendyslexic?
					</BS.Col>
					<BS.Col
						xs={12}
						lg={4}
						className="mx-1 mb-4 d-flex align-items-center row"
					>
						{localStorage.getItem("twemoji") ? (
							<Toggle
								className="mr-2"
								defaultChecked={true}
								icons={false}
								onChange={() => {
									localStorage.removeItem("twemoji");
									forceUpdate();
								}}
							/>
						) : (
							<Toggle
								className="mr-2"
								defaultChecked={false}
								icons={false}
								onChange={() => {
									localStorage.setItem("twemoji", "true");
									forceUpdate();
								}}
							/>
						)}
						Use twemoji?
					</BS.Col>
					<BS.Col
						xs={12}
						lg={4}
						className="mx-1 mb-4 d-flex align-items-center row"
					>
						{localStorage.getItem("pagesonly") ? (
							<Toggle
								className="mr-2"
								defaultChecked={true}
								icons={false}
								onChange={() => {
									localStorage.removeItem("pagesonly");
									forceUpdate();
								}}
							/>
						) : (
							<Toggle
								className="mr-2"
								defaultChecked={false}
								icons={false}
								onChange={() => {
									localStorage.setItem("pagesonly", "true");
									forceUpdate();
								}}
							/>
						)}
						Use only member pages?
					</BS.Col>
					<BS.Col
						xs={12}
						lg={4}
						className="mx-1 mb-4 d-flex align-items-center row"
					>
						{localStorage.getItem("fullbg") ? (
							<Toggle
								className="mr-2"
								defaultChecked={true}
								icons={false}
								onChange={() => {
									localStorage.removeItem("fullbg");
									forceUpdate();
								}}
							/>
						) : (
							<Toggle
								className="mr-2"
								defaultChecked={false}
								icons={false}
								onChange={() => {
									localStorage.setItem("fullbg", "true");
									forceUpdate();
								}}
							/>
						)}
						Remove gradient from background color?
					</BS.Col>
					<BS.Col
						xs={12}
						lg={4}
						className="mx-1 mb-4 d-flex align-items-center row"
					>
						{localStorage.getItem("colorbg") ? (
							<Toggle
								className="mr-2"
								defaultChecked={true}
								icons={false}
								onChange={() => {
									localStorage.removeItem("colorbg");
									forceUpdate();
								}}
							/>
						) : (
							<Toggle
								className="mr-2"
								defaultChecked={false}
								icons={false}
								onChange={() => {
									localStorage.setItem("colorbg", "false");
									forceUpdate();
								}}
							/>
						)}
						Hide colored backgrounds altogether?
					</BS.Col>
					<BS.Col
						xs={12}
						lg={4}
						className="mx-1 mb-4 d-flex align-items-center row"
					>
						{localStorage.getItem("expandcards") ? (
							<Toggle
								className="mr-2"
								defaultChecked={true}
								icons={false}
								onChange={() => {
									localStorage.removeItem("expandcards");
									forceUpdate();
								}}
							/>
						) : (
							<Toggle
								className="mr-2"
								defaultChecked={false}
								icons={false}
								onChange={() => {
									localStorage.setItem("expandcards", "true");
									forceUpdate();
								}}
							/>
						)}
						Expand member cards on default?
					</BS.Col>
					<BS.Col
						xs={12}
						lg={4}
						className="mx-1 mb-4 d-flex align-items-center row"
					>
						{localStorage.getItem("hidebanners") ? (
							<Toggle
								className="mr-2"
								defaultChecked={true}
								icons={false}
								onChange={() => {
									localStorage.removeItem("hidebanners");
									forceUpdate();
								}}
							/>
						) : (
							<Toggle
								className="mr-2"
								defaultChecked={false}
								icons={false}
								onChange={() => {
									localStorage.setItem("hidebanners", "true");
									forceUpdate();
								}}
							/>
						)}
						Hide banners at the top?
					</BS.Col>
					<BS.Col
						xs={12}
						lg={4}
						className="mx-1 mb-4 d-flex align-items-center row"
					>
						{localStorage.getItem("bottombanners") ? (
							<Toggle
								className="mr-2"
								defaultChecked={true}
								icons={false}
								onChange={() => {
									localStorage.removeItem("bottombanners");
									forceUpdate();
								}}
							/>
						) : (
							<Toggle
								className="mr-2"
								defaultChecked={false}
								icons={false}
								onChange={() => {
									localStorage.setItem("bottombanners", "true");
									forceUpdate();
								}}
							/>
						)}
						Show banners at the bottom?
					</BS.Col>
				</BS.Row>
			</BS.Card.Body>
		</BS.Card>
	);
};

export default Settings;
