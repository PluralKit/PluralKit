import React, { useEffect, useState, useCallback } from 'react';
import { useRouteMatch, Switch, Route } from "react-router-dom";
import  * as BS from 'react-bootstrap'
import Popup from 'reactjs-popup';
import 'reactjs-popup/dist/index.css';
import { useForm } from "react-hook-form";

import MemberCard from './MemberCard.js'
import MemberPages from '../../Pages/MemberPages.js'
import Loading from "../Loading.js";
import API_URL from "../../Constants/constants.js";

import { FaPlus } from "react-icons/fa";

export default function Memberlist() {

	const { path } = useRouteMatch();
	
	const user = JSON.parse(localStorage.getItem('user'));
	const userId = user.id;

	const [isLoading, setIsLoading ] = useState(false);
	const [isError, setIsError ] = useState(false);
	const [errorAlert, setErrorAlert ] = useState(false);

	const [proxyView, setProxyView] = useState(false);
	const [privacyView, setPrivacyView] = useState(false);
	const [currentPage, setCurrentPage] = useState(1);
	const [membersPerPage, setMembersPerPage] = useState(25);

	const [open, setOpen] = useState(false);
	const closeModal = () => setOpen(false);

	const [members, setMembers ] = useState([]);

	const [searchBy, setSearchBy] = useState('name')
	const [privacyFilter, setPrivacyFilter] = useState('all')
	const [sortBy, setSortBy] = useState('name')
	const [sortOrder, setSortOrder] = useState('ascending')

	const [value, setValue] = useState('');
	const [proxyTags, setProxyTags] = useState([{
		prefix: "", suffix: ""
	}]);

	const {register, handleSubmit, setValue: setTemplate} = useForm();

	const fetchMembers = useCallback( () => {
		setIsLoading(true);
		setIsError(false);
		setMembersPerPage(localStorage.getItem("expandcards") ? 10 : 25);

		 fetch(`${API_URL}s/${userId}/members`,{
		method: 'GET',
		headers: {
			'Authorization': localStorage.getItem("token")
		}}).then ( res => res.json()
		).then (data => { 
			setMembers(data)
			setIsLoading(false);
	})
		.catch (error => { 
				console.log(error);
				setIsError(true);
				setIsLoading(false);
		})
	}, [userId])

	useEffect(() => {
		fetchMembers();
	}, [fetchMembers])

	const indexOfLastMember = currentPage * membersPerPage;
	const indexOfFirstMember = indexOfLastMember - membersPerPage;

		let Members = members.map(member => {
			if (member.display_name) {
				return {...member, displayName: member.display_name}
			} return {...member, displayName: member.name}
		}) 
		let Members1 = Members.map(member => {
			if (member.description) {
				return {...member, desc: member.description}
			} return {...member, desc: "(no description)"}
		})

		const currentMembers = Members1.filter(member => {
			if (!value & privacyFilter === 'all') return true;
			
			if (privacyFilter === 'private') {
				if (member.visibility !== 'private') {
					return false;
				}
			} else if (privacyFilter === 'public') {
				if (member.visibility !== 'public') {
					return false;
				}
			}

			if (searchBy === 'name') {
			if (member.name.toLowerCase().includes(value.toLowerCase())) {
				return true;
			}
			return false;
		} else if (searchBy === 'display name') {
			if (member.displayName.toLowerCase().includes(value.toLowerCase())) {
				return true;
			}
			return false
		} else if (searchBy === 'description') {
			if (member.desc.toLowerCase().includes(value.toLowerCase())) {
				return true;
			}
			return false;
		} else if (searchBy === 'ID') {
			if (member.id.toLowerCase().includes(value.toLowerCase())) {
				return true;
			}
			return false;
		}

			return false;
		})

		const active = currentPage;
		const pageAmount = Math.ceil(currentMembers.length / membersPerPage);

			var sortMembers = currentMembers;
			if (sortBy === 'name') {
				if (sortOrder === 'descending') {
					sortMembers =  currentMembers.sort((a, b) => a.name.localeCompare(b.name)).reverse().slice(indexOfFirstMember, indexOfLastMember);
				} else sortMembers =  currentMembers.sort((a, b) => a.name.localeCompare(b.name)).slice(indexOfFirstMember, indexOfLastMember);
			} 
			else if (sortBy === 'display name') {
					if (sortOrder === 'descending') {
						sortMembers =  currentMembers.sort((a, b) => a.displayName.localeCompare(b.displayName)).reverse().slice(indexOfFirstMember, indexOfLastMember);
					} else sortMembers =  currentMembers.sort((a, b) => a.displayName.localeCompare(b.displayName)).slice(indexOfFirstMember, indexOfLastMember);
			} 
			else if (sortBy === 'ID') {
				if (sortOrder === 'descending') {
					sortMembers =  currentMembers.sort((a, b) => a.id.localeCompare(b.id)).reverse().slice(indexOfFirstMember, indexOfLastMember);
				} else sortMembers =  currentMembers.sort((a, b) => a.id.localeCompare(b.id)).slice(indexOfFirstMember, indexOfLastMember);
			} 
			else if (sortBy === 'date created') {
				if (sortOrder === 'descending') {
					sortMembers =  currentMembers.sort((a, b) => a.created.localeCompare(b.created)).reverse().slice(indexOfFirstMember, indexOfLastMember);
				} else sortMembers =  currentMembers.sort((a, b) => a.created.localeCompare(b.created)).slice(indexOfFirstMember, indexOfLastMember);
			}

			const memberList = sortMembers.map((member) => <BS.Card key={member.id} className={localStorage.getItem("expandcards") ? "mb-3" : ""}>
			<MemberCard
			member={member} 
			edit={memberEdit => setMembers(members.map(member => member.id === memberEdit.id ? Object.assign(member, memberEdit) : member))}
			/>
		</BS.Card>
		);

		function addProxyField() {
			setProxyTags(oldTags => [...oldTags, {prefix: '', suffix: ''}] )
		}

		const submitMember = data => {
			setIsLoading(true);

			const newdata = data.proxy_tags ? {...data, proxy_tags: data.proxy_tags.filter(tag => !(tag.prefix === "" && tag.suffix === ""))} : data

			fetch(`${API_URL}m/`,{
				method: 'POST',
				body: JSON.stringify(newdata),
				headers: {
					'Content-Type': 'application/json',
					'Authorization': localStorage.getItem("token")
				}}).then (res => res.json()
				).then (data => { 
						setErrorAlert(false);
						closeModal();
						fetchMembers();
		}
				).catch (error => {
						console.error(error);
						setErrorAlert(true);
				});
		}

		return (
			<Switch>
				<Route exact path={path}>
				<>
				<BS.Row className="mb-lg-3 justfiy-content-md-center">
				<BS.Col xs={12} lg={3}>
				<BS.Form>
					<BS.InputGroup className="mb-3">
					<BS.Form.Control disabled placeholder='Page length:'/>
						<BS.Form.Control as="select" defaultValue={localStorage.getItem("expandcards") ? 10 : 25} onChange={e => {
							setMembersPerPage(e.target.value);
							setCurrentPage(1);
							}}>
							<option>10</option>
							<option>25</option>
							<option>50</option>
							<option>100</option>
						</BS.Form.Control>
						</BS.InputGroup>
				</BS.Form>
				</BS.Col>
				<BS.Col xs={12} lg={3}>
				<BS.Form>
					<BS.InputGroup className="mb-3">
					<BS.Form.Control disabled placeholder='Search by:'/>
						<BS.Form.Control as="select" defaultValue={searchBy} onChange={e => {
							setSearchBy(e.target.value)
							}}>
							<option>name</option>
							<option>display name</option>
							<option>description</option>
							<option>ID</option>
						</BS.Form.Control>
						</BS.InputGroup>
				</BS.Form>
				</BS.Col>
				<BS.Col xs={12} lg={3}>
				<BS.Form>
					<BS.InputGroup className="mb-3">
					<BS.Form.Control disabled placeholder='Sort by:'/>
						<BS.Form.Control as="select" defaultValue={sortBy} onChange={e => {
							setSortBy(e.target.value)
							}}>
							<option>name</option>
							<option>display name</option>
							<option>ID</option>
							<option>date created</option>
						</BS.Form.Control>
						</BS.InputGroup>
				</BS.Form>
				</BS.Col>
				<BS.Col xs={12} lg={3}>
				<BS.Form>
					<BS.InputGroup className="mb-3">
					<BS.Form.Control disabled placeholder='Sort order:'/>
						<BS.Form.Control as="select" defaultValue={sortOrder} onChange={e => {
							setSortOrder(e.target.value)
							}}>
							<option>ascending</option>
							<option>descending</option>
						</BS.Form.Control>
						</BS.InputGroup>
				</BS.Form>
				</BS.Col>
				</BS.Row>
				<BS.Row className="justify-content-md-center">
				<BS.Col xs={12} lg={3}>
				<BS.Form>
					<BS.InputGroup className="mb-3">
					<BS.Form.Control disabled placeholder='Only show:'/>
						<BS.Form.Control as="select" defaultValue={privacyFilter} onChange={e => {
							setPrivacyFilter(e.target.value)
							}}>
							<option>all</option>
							<option>private</option>
							<option>public</option>
						</BS.Form.Control>
						</BS.InputGroup>
				</BS.Form>
				</BS.Col>
				<BS.Col className="mb-3" xs={12} lg={7}>
				<BS.Form>
						<BS.Form.Control value={value} onChange={e => {setValue(e.target.value); setCurrentPage(1);}} placeholder={`Search by ${searchBy}`}/>
				</BS.Form>
				</BS.Col>
				<BS.Col className="mb-3" xs={12} lg={2}>
					<BS.Button type="primary" className="m-0" block onClick={() => fetchMembers()}>Refresh</BS.Button>
				</BS.Col>
				</BS.Row>
				<BS.Row className="justify-content-md-center">
				<BS.Pagination className="ml-auto mr-auto">
					{ currentPage === 1 ? <BS.Pagination.Prev disabled/> : <BS.Pagination.Prev onClick={() => setCurrentPage(currentPage - 1)} />}
					{ currentPage < 3 ? "" : <BS.Pagination.Item  onClick={() => setCurrentPage(1)} active={1 === active}>{1}</BS.Pagination.Item>}
					{ currentPage < 4 ? "" : currentPage < 5 ? <BS.Pagination.Item  onClick={() => setCurrentPage(2)} active={2 === active}>{2}</BS.Pagination.Item> : <BS.Pagination.Ellipsis disabled />}
					{ currentPage > 1 ? <BS.Pagination.Item  onClick={() => setCurrentPage(currentPage - 1)}>{currentPage - 1}</BS.Pagination.Item> : "" }
					<BS.Pagination.Item  onClick={() => setCurrentPage(currentPage)} active={currentPage === active}>{currentPage}</BS.Pagination.Item>
					{ currentPage < pageAmount ? <BS.Pagination.Item  onClick={() => setCurrentPage(currentPage + 1)}>{currentPage + 1}</BS.Pagination.Item> : "" }
					{ currentPage > pageAmount - 3 ? "" : currentPage === pageAmount - 3 ? <BS.Pagination.Item  onClick={() => setCurrentPage(pageAmount - 1)} active={pageAmount - 1 === active}>{pageAmount - 1}</BS.Pagination.Item> : <BS.Pagination.Ellipsis disabled />}
					{ currentPage > pageAmount - 2 ? "" : <BS.Pagination.Item  onClick={() => setCurrentPage(pageAmount)} active={pageAmount === active}>{pageAmount}</BS.Pagination.Item>}
					{ currentPage === pageAmount ? <BS.Pagination.Next disabled /> :<BS.Pagination.Next onClick={() => setCurrentPage(currentPage + 1)} />}
					</BS.Pagination>
				</BS.Row>
				{ isLoading ? <Loading /> : isError ? 
				<BS.Alert variant="danger">Error fetching members.</BS.Alert> :
				<>
				<BS.Card className="w-100">
					<BS.Card.Header className="d-flex align-items-center justify-content-between">
						<BS.Button variant="link" className="float-left" onClick={() => setOpen(o => !o)}><FaPlus className="mr-4"/>Add Member</BS.Button> 
						<Popup open={open} position="top-center" modal>
							<BS.Container>
								<BS.Card>
									<BS.Card.Header>
											<h5><FaPlus className="mr-3"/> Add member </h5>
									</BS.Card.Header>
									<BS.Card.Body>
									{ errorAlert ? <BS.Alert variant="danger">Something went wrong, please try logging in and out again.</BS.Alert> : "" }
										<BS.Form onSubmit={handleSubmit(submitMember)}>
														<BS.Form.Text>
														</BS.Form.Text>
														<BS.Form.Row>
												<BS.Col className="mb-lg-2" xs={12} lg={3}>
														<BS.Form.Label>Name:</BS.Form.Label>
													<BS.Form.Control name="name"{...register("name")} defaultValue={''} required/>
												</BS.Col>
												<BS.Col className="mb-lg-2" xs={12} lg={3}>
														<BS.Form.Label>Display name: </BS.Form.Label>
														<BS.Form.Control name="display_name" {...register("display_name")}  defaultValue={''} />
												</BS.Col>
												<BS.Col className="mb-lg-2" xs={12} lg={3}>
														<BS.Form.Label>Birthday:</BS.Form.Label>
														<BS.Form.Control  pattern="^\d{4}\-(0[1-9]|1[012])\-(0[1-9]|[12][0-9]|3[01])$" name="birthday" {...register("birthday")}  defaultValue={''}/>
														<BS.Form.Text>(YYYY-MM-DD)</BS.Form.Text>
												</BS.Col>
												<BS.Col className="mb-lg-2" xs={12} lg={3}>
														<BS.Form.Label>Pronouns:</BS.Form.Label>
														<BS.Form.Control as="textarea" rows="1" name="pronouns" {...register("pronouns")} defaultValue={''} />
												</BS.Col>
												<BS.Col className="mb-lg-2" xs={12} lg={3}>
														<BS.Form.Label>Avatar url:</BS.Form.Label> 
													<BS.Form.Control type="url" name="avatar_url" {...register("avatar_url")}  defaultValue={''} />
												</BS.Col>
												<BS.Col className="mb-lg-2" xs={12} lg={3}>
														<BS.Form.Label>Banner url:</BS.Form.Label> 
													<BS.Form.Control type="url" name="banner" {...register("banner")}  defaultValue={''} />
												</BS.Col>
												<BS.Col className="mb-lg-2" xs={12} lg={3}>
														<BS.Form.Label>Color:</BS.Form.Label> 
													<BS.Form.Control  pattern="[A-Fa-f0-9]{6}" name="color" {...register("color")}  defaultValue={''} />
														<BS.Form.Text>(hexcode)</BS.Form.Text>
												</BS.Col>
												<BS.Col className="mb-lg-2" xs={12} lg={2}>
														<BS.Form.Label>Proxy tags:</BS.Form.Label> 
														<BS.Button variant="primary" block onClick={() => setProxyView(view => !view)}> { proxyView ? "Hide" : "Show" }</BS.Button>
												</BS.Col>
												<BS.Col className="mb-lg-2" xs={12} lg={2}>
														<BS.Form.Label>Privacy settings:</BS.Form.Label> 
														<BS.Button variant="primary" block onClick={() => setPrivacyView(view => !view)}> { privacyView ? "Hide" : "Show" }</BS.Button>
												</BS.Col>
										</BS.Form.Row>
										<hr/>
										{ proxyView ? <>
								<h5>Proxy Tags</h5>
										<BS.Form.Row>
									{ proxyTags.map((item, index) => (
										<BS.Col key={index} className="mb-lg-2" xs={12} lg={6}>
												<BS.Form.Row>
												<BS.InputGroup className="ml-1 mr-1 mb-1">
												<BS.Form.Control as="textarea" rows="1" name={`proxy_tags[${index}].prefix`} defaultValue={item.prefix} {...register(`proxy_tags[${index}].prefix`)}/> 
												<BS.Form.Control style={{flex: '0 0 3.5em'}} as="textarea" rows="1" disabled placeholder='text'/>
												<BS.Form.Control as="textarea" rows="1" name={`proxy_tags[${index}].suffix`} defaultValue={item.suffix} {...register(`proxy_tags[${index}].suffix`)}/>
												</BS.InputGroup>
												</BS.Form.Row>
										</BS.Col>
								))} <BS.Col className="mb-lg-2" xs={12} lg={2}><BS.Button block variant="light" onClick={() => addProxyField()}>Add new</BS.Button></BS.Col>
						 </BS.Form.Row>
										<hr/></> : "" }
								{ privacyView ? <><h5>privacy settings</h5>
										<BS.Form.Row>
										<BS.Col className="mb-lg-2" xs={12} lg={3}>
												<BS.Form.Label>Visibility:</BS.Form.Label>
												<BS.Form.Control name="visibility" as="select" {...register("visibility")} >
														<option>public</option>
														<option>private</option>
												</BS.Form.Control>
										</BS.Col>
										<BS.Col className="mb-lg-2" xs={12} lg={3}>
										<BS.Form.Label>Name:</BS.Form.Label>
												<BS.Form.Control name="name_privacy" as="select" {...register("name_privacy")} >
														<option>public</option>
														<option>private</option>
												</BS.Form.Control>
										</BS.Col>
										<BS.Col className="mb-lg-2" xs={12} lg={3}>
										<BS.Form.Label>Description:</BS.Form.Label>
												<BS.Form.Control name="description_privacy" as="select" {...register("description_privacy")} >
														<option>public</option>
														<option>private</option>
												</BS.Form.Control>
										</BS.Col>
										<BS.Col className="mb-lg-2" xs={12} lg={3}>
										<BS.Form.Label>Birthday:</BS.Form.Label>
												<BS.Form.Control name="birthday_privacy" as="select" {...register("birthday_privacy")} >
														<option>public</option>
														<option>private</option>
												</BS.Form.Control>
										</BS.Col>
										<BS.Col className="mb-lg-2" xs={12} lg={3}>
										<BS.Form.Label>Pronouns:</BS.Form.Label>
												<BS.Form.Control name="pronoun_privacy" as="select" {...register("pronoun_privacy")} >
														<option>public</option>
														<option>private</option>
												</BS.Form.Control>
										</BS.Col>
										<BS.Col className="mb-3" xs={12} lg={3}>
										<BS.Form.Label>Meta:</BS.Form.Label>
												<BS.Form.Control name="metadata_privacy" as="select" {...register("metadata_privacy")} >
														<option>public</option>
														<option>private</option>
												</BS.Form.Control>
										</BS.Col>
								</BS.Form.Row>               
								<hr/></> : "" }
								<BS.Form.Group className="mt-3">
												<BS.Form.Label>Description:</BS.Form.Label><br/>
												{ localStorage.getItem('template1') ? <BS.Button className="mb-2" size="sm" variant="primary" onClick={() => setTemplate('description', localStorage.getItem('template1'))}>Template 1</BS.Button> : ""} { localStorage.getItem('template2') ? <BS.Button className="mb-2" size="sm" variant="primary" onClick={() => setTemplate('description', localStorage.getItem('template2'))}>Template 2</BS.Button> : ""} { localStorage.getItem('template3') ? <BS.Button className="mb-2" size="sm" variant="primary" onClick={() => setTemplate('description', localStorage.getItem('template3'))}>Template 3</BS.Button> : ""}
												<BS.Form.Control maxLength="1000" as="textarea" rows="7" name="description" {...register("description")}  defaultValue={''}/>
										</BS.Form.Group>
										<BS.Button variant="primary" type="submit">Submit</BS.Button> <BS.Button variant="light" className="float-right" onClick={closeModal}>Cancel</BS.Button>
										</BS.Form>
									</BS.Card.Body>
								</BS.Card>
							</BS.Container>
						</Popup>
					</BS.Card.Header>
				</BS.Card>
				
				<BS.Accordion className="mb-3 mt-3 w-100" defaultActiveKey="0">
						{memberList}
				</BS.Accordion>
				<BS.Row className="justify-content-md-center">
				<BS.Pagination className="ml-auto mr-auto">
					{ currentPage === 1 ? <BS.Pagination.Prev disabled/> : <BS.Pagination.Prev onClick={() => setCurrentPage(currentPage - 1)} />}
					{ currentPage < 3 ? "" : <BS.Pagination.Item  onClick={() => setCurrentPage(1)} active={1 === active}>{1}</BS.Pagination.Item>}
					{ currentPage < 4 ? "" : currentPage < 5 ? <BS.Pagination.Item  onClick={() => setCurrentPage(2)} active={2 === active}>{2}</BS.Pagination.Item> : <BS.Pagination.Ellipsis disabled />}
					{ currentPage > 1 ? <BS.Pagination.Item  onClick={() => setCurrentPage(currentPage - 1)}>{currentPage - 1}</BS.Pagination.Item> : "" }
					<BS.Pagination.Item  onClick={() => setCurrentPage(currentPage)} active={currentPage === active}>{currentPage}</BS.Pagination.Item>
					{ currentPage < pageAmount ? <BS.Pagination.Item  onClick={() => setCurrentPage(currentPage + 1)}>{currentPage + 1}</BS.Pagination.Item> : "" }
					{ currentPage > pageAmount - 3 ? "" : currentPage === pageAmount - 3 ? <BS.Pagination.Item  onClick={() => setCurrentPage(pageAmount - 1)} active={pageAmount - 1 === active}>{pageAmount - 1}</BS.Pagination.Item> : <BS.Pagination.Ellipsis disabled />}
					{ currentPage > pageAmount - 2 ? "" : <BS.Pagination.Item  onClick={() => setCurrentPage(pageAmount)} active={pageAmount === active}>{pageAmount}</BS.Pagination.Item>}
					{ currentPage === pageAmount ? <BS.Pagination.Next disabled /> :<BS.Pagination.Next onClick={() => setCurrentPage(currentPage + 1)} />}
					</BS.Pagination>
				</BS.Row>
				</>
				}
				</>
				</Route>
				<Route path={`${path}/:memberID`}>
					{ isLoading ? <Loading/> :
					<MemberPages members={members}
					edit={memberEdit => setMembers(members.map(member => member.id === memberEdit.id ? Object.assign(member, memberEdit) : member))}/>}
				</Route>
				</Switch>
		)
}