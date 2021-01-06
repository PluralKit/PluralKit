import React, { useEffect, useState, useCallback } from 'react';
import { Switch, Route, useParams, useRouteMatch } from 'react-router-dom';
import  * as BS from 'react-bootstrap'
import 'reactjs-popup/dist/index.css';

import ProfileCard from './ProfileCard.js'
import ProfilePages from './ProfilePages.js'
import Loading from "./Loading.js";
import API_URL from "../Constants/constants.js";

export default function Memberlist() {

    const { path } = useRouteMatch();
    const { sysID } = useParams();

    const [isLoading, setIsLoading ] = useState(false);
    const [isError, setIsError ] = useState(false);

    const [currentPage, setCurrentPage] = useState(1);
    const [membersPerPage, setMembersPerPage] = useState(25);

    const [members, setMembers ] = useState([]);
    const [memberData, setMemberData ] = useState([]);
    const [filteredMembers, setFilteredMembers ] = useState([]);
    const [sortedMembers, setSortedMembers ] = useState([]);

    const [searchBy, setSearchBy] = useState('name')
    const [sortBy, setSortBy] = useState('name')

    const [value, setValue] = useState('');

  const fetchMembers = useCallback( () => {
    setIsLoading(true);
    setIsError(false);
    setMembersPerPage(25);

     fetch(`${API_URL}s/${sysID}/members`,{
    method: 'GET',
    }).then ( res => res.json()
    ).then (data => { 
    setMembers(data)
      setIsLoading(false);
  })
    .catch (error => { 
        console.log(error);
        setIsError(true);
        setIsLoading(false);
    })
  }, [sysID])

  useEffect(() => {
    fetchMembers();
  }, [fetchMembers])

  useEffect(() => {
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
    setMemberData(Members1);
  }, [members])

    const indexOfLastMember = currentPage * membersPerPage;
    const indexOfFirstMember = indexOfLastMember - membersPerPage;

    useEffect(() => {
      searchMembers();
    }, [value, memberData, searchBy])

    function searchMembers() {
    const currentMembers =  memberData.filter(member => {
      if (!value) return true;
      
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
    setFilteredMembers(currentMembers);
    }

    useEffect (() => { 
      if (sortBy === 'name') {
      const sortMembers =  filteredMembers.sort((a, b) => a.name.localeCompare(b.name)).slice(indexOfFirstMember, indexOfLastMember);
      setSortedMembers(sortMembers);
      } else if (sortBy === 'display name') {
        const sortMembers =  filteredMembers.sort((a, b) => a.displayName.localeCompare(b.displayName)).slice(indexOfFirstMember, indexOfLastMember);
        setSortedMembers(sortMembers);
      } else if (sortBy === 'ID') {
        const sortMembers =  filteredMembers.sort((a, b) => a.id.localeCompare(b.id)).slice(indexOfFirstMember, indexOfLastMember);
        setSortedMembers(sortMembers);
      }
    }, [sortBy, filteredMembers, indexOfFirstMember, indexOfLastMember])
   

    const active = currentPage;
    const pageAmount = Math.ceil(filteredMembers.length / membersPerPage);
      
      const memberList = sortedMembers.map((member) => <BS.Card key={member.id}>
        <ProfileCard
        member={member} 
        />
    </BS.Card>
    );

    return (
      <Switch>
        <Route exact path={path}>
      <>
      <BS.Row className="mb-3 justfiy-content-md-center">
      <BS.Col xs={12} lg={4}>
      <BS.Form>
        <BS.InputGroup className="mb-3">
        <BS.Form.Control disabled placeholder='Page length:'/>
          <BS.Form.Control as="select" defaultValue="25" onChange={e => {
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
      <BS.Col xs={12} lg={4}>
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
      <BS.Col xs={12} lg={4}>
      <BS.Form>
        <BS.InputGroup className="mb-3">
        <BS.Form.Control disabled placeholder='Sort by:'/>
          <BS.Form.Control as="select" defaultValue={sortBy} onChange={e => {
            setSortBy(e.target.value)
            }}>
            <option>name</option>
            <option>display name</option>
            <option>ID</option>
          </BS.Form.Control>
          </BS.InputGroup>
      </BS.Form>
      </BS.Col>
      </BS.Row>
      <BS.Row noGutters="true" className="justify-content-md-center">
      <BS.Col className="ml-lg-2 mb-3" xs={12} lg={6}>
      <BS.Form>
          <BS.Form.Control value={value} onChange={e => {setValue(e.target.value); setCurrentPage(1);}} placeholder={`Search by ${searchBy}`}/>
      </BS.Form>
      </BS.Col>
      <BS.Col className="ml-lg-2 mb-3" xs={12} lg={1}>
        <BS.Button type="primary" className="m-0" block onClick={() => fetchMembers()}>Refresh</BS.Button>
      </BS.Col>
      </BS.Row>
      <BS.Row className="justify-content-md-center">
      <BS.Pagination className="ml-auto mr-auto">
        { currentPage === 1 ? <BS.Pagination.Prev disabled/> : <BS.Pagination.Prev onClick={() => setCurrentPage(currentPage - 1)} />}
        { currentPage < 3 ? "" : <BS.Pagination.Item  onClick={() => setCurrentPage(1)} active={1 === active}>{1}</BS.Pagination.Item>}
        { currentPage < 4 ? "" :<BS.Pagination.Ellipsis disabled />}
        { currentPage > 1 ? <BS.Pagination.Item  onClick={() => setCurrentPage(currentPage - 1)}>{currentPage - 1}</BS.Pagination.Item> : "" }
        <BS.Pagination.Item  onClick={() => setCurrentPage(currentPage)} active={currentPage === active}>{currentPage}</BS.Pagination.Item>
        { currentPage < pageAmount ? <BS.Pagination.Item  onClick={() => setCurrentPage(currentPage + 1)}>{currentPage + 1}</BS.Pagination.Item> : "" }
        { currentPage > pageAmount - 3 ? "" : <BS.Pagination.Ellipsis disabled />}
        { currentPage > pageAmount - 2 ? "" : <BS.Pagination.Item  onClick={() => setCurrentPage(pageAmount)} active={pageAmount === active}>{pageAmount}</BS.Pagination.Item>}
        { currentPage === pageAmount ? <BS.Pagination.Next disabled /> :<BS.Pagination.Next onClick={() => setCurrentPage(currentPage + 1)} />}
        </BS.Pagination>
      </BS.Row>
      { isLoading ? <Loading /> : isError ? 
      <BS.Alert variant="danger">Error fetching members.</BS.Alert> :
      <>
        <BS.Accordion className="mb-3 mt-3 w-100" defaultActiveKey="0">
            {memberList}
        </BS.Accordion>
        </>
        }
        </>
          </Route>
            <Route path={`/pk-webs/profile/${sysID}/:memberID`}>
              { isLoading ? <Loading/> :
            <ProfilePages members={members}/>}
          </Route>
        </Switch>
    )
}