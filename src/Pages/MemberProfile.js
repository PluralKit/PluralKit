import React, { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import  * as BS from 'react-bootstrap';

import Loading from '../Components/Loading';
import API_URL from '../Constants/constants';
import ProfilePage from '../Components/Public/ProfilePage';

const MemberProfile = () => {
		const { memberID } = useParams();

		const [isLoading, setIsLoading ] = useState(false);
		const [isError, setIsError ] = useState(false);
		const [isForbidden, setIsForbidden ] = useState(false);
		const [ member, setMember ] = useState({});

		const fetchMember = useCallback( () => {
				setIsLoading(true);
				setIsError(false);
		
				 fetch(`${API_URL}m/${memberID}`,{
				method: 'GET',
				}).then ( res => {
					if (res.status === 403) {
						throw new Error('Access denied!');
					}
					return res.json()
				}
				).then (data => { 
				setMember(data)
					setIsLoading(false);
			})
				.catch (error => {
					if (error.message === 'Access denied!') {
						setIsForbidden(true);
					} else {
						console.log(error);
						setIsError(true);
					}
					setIsLoading(false);
				})
			}, [memberID])
		
			useEffect(() => {
				fetchMember();
			}, [fetchMember])

			return (
					isLoading ? <Loading /> : isError ? 
					<BS.Alert variant="danger">Error fetching member.</BS.Alert> : isForbidden ? <BS.Alert variant="danger">This member is private.</BS.Alert> : <ProfilePage member={member} list={false}/>
			);
}

export default MemberProfile;